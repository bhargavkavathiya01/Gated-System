-- ============================================================
-- Migration: Security Approval Flow
--
-- Adds an approval-based flow for security personnel, mirroring
-- the flat owner request flow.
--
-- 1. tblsecurityrequest table
-- 2. sp_api_securityrequest        (op 2 = create, op 3 = approve/reject)
-- 3. sp_api_getsecurityrequests    (list with optional status filter)
-- 4. sp_api_getsecurityrequestbyid (single record)
--
-- Existing flows (createsecretaryorsecurity direct assignment) are UNCHANGED.
-- Run in pgAdmin against nandisystem database.
-- ============================================================


-- ── 1. Table ─────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS public.tblsecurityrequest (
    id                SERIAL PRIMARY KEY,
    propertyid        INTEGER NOT NULL,
    userid            INTEGER NOT NULL,
    roleid            INTEGER NOT NULL,
    requestedby       INTEGER NOT NULL,
    status            TEXT NOT NULL DEFAULT 'Pending',
    approvedby        INTEGER,
    approvedon        TIMESTAMP,
    rejectionreason   TEXT,
    createdon         TIMESTAMP NOT NULL DEFAULT NOW(),
    aadharcard        TEXT,
    appointmentletter TEXT
);


-- ── 2. sp_api_securityrequest ─────────────────────────────────────────
-- op 2: Create pending request + COALESCE-update user docs in tblusermaster
-- op 3: Approve/Reject — on Approved, call sp_api_userrolemaster to assign role
-- ──────────────────────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION public.sp_api_securityrequest(
    p_operation INTEGER,
    p_json      JSONB DEFAULT NULL
)
RETURNS JSON
LANGUAGE plpgsql
AS $$
DECLARE
    v_id              integer;
    v_requestid       integer;
    v_status          text;
    v_approvedby      integer;
    v_rejectionreason text;
    v_userid          integer;
    v_propertyid      integer;
    v_roleid          integer;
    v_aadharcard      text;
    v_appointmentletter text;
    v_role_result     json;
    v_role_status     integer;
    v_error_message   text;
    v_error_code      text;
    v_json            jsonb;
BEGIN
    -- ── op 2: Create request ─────────────────────────────────────────
    IF p_operation = 2 THEN
        IF (p_json->>'propertyid')::integer IS NULL  THEN RAISE EXCEPTION 'PropertyId is required';  END IF;
        IF (p_json->>'userid')::integer IS NULL       THEN RAISE EXCEPTION 'UserId is required';      END IF;
        IF (p_json->>'roleid')::integer IS NULL       THEN RAISE EXCEPTION 'RoleId is required';      END IF;
        IF (p_json->>'requestedby')::integer IS NULL  THEN RAISE EXCEPTION 'RequestedBy is required'; END IF;

        INSERT INTO public.tblsecurityrequest (
            propertyid, userid, roleid, requestedby, status, createdon,
            aadharcard, appointmentletter
        ) VALUES (
            (p_json->>'propertyid')::integer,
            (p_json->>'userid')::integer,
            (p_json->>'roleid')::integer,
            (p_json->>'requestedby')::integer,
            'Pending',
            NOW(),
            NULLIF(p_json->>'aadharcard', ''),
            NULLIF(p_json->>'appointmentletter', '')
        ) RETURNING id INTO v_id;

        -- Preserve existing user docs; only overwrite when new ones provided
        UPDATE public.tblusermaster
        SET aadharcard        = COALESCE(NULLIF(p_json->>'aadharcard', ''),        aadharcard),
            appointmentletter = COALESCE(NULLIF(p_json->>'appointmentletter', ''), appointmentletter)
        WHERE id = (p_json->>'userid')::integer;

        RETURN json_build_object(
            'status_code', 201,
            'message', 'Security request created successfully',
            'data', json_build_object('id', v_id)
        );

    -- ── op 3: Approve / Reject ────────────────────────────────────────
    ELSIF p_operation = 3 THEN
        IF (p_json->>'requestid')::integer IS NULL   THEN RAISE EXCEPTION 'RequestId is required';  END IF;
        IF p_json->>'status' IS NULL OR p_json->>'status' = '' THEN RAISE EXCEPTION 'Status is required'; END IF;
        IF (p_json->>'approvedby')::integer IS NULL  THEN RAISE EXCEPTION 'ApprovedBy is required'; END IF;

        v_requestid       := (p_json->>'requestid')::integer;
        v_status          := p_json->>'status';
        v_approvedby      := (p_json->>'approvedby')::integer;
        v_rejectionreason := NULLIF(p_json->>'rejectionreason', '');

        IF v_status NOT IN ('Pending', 'Approved', 'Rejected') THEN
            RAISE EXCEPTION 'Invalid status. Must be Pending, Approved, or Rejected';
        END IF;

        -- Fetch request details before updating (needed for role assignment)
        SELECT userid, propertyid, roleid, aadharcard, appointmentletter
        INTO   v_userid, v_propertyid, v_roleid, v_aadharcard, v_appointmentletter
        FROM   public.tblsecurityrequest
        WHERE  id = v_requestid;

        IF NOT FOUND THEN RAISE EXCEPTION 'Security request not found'; END IF;

        UPDATE public.tblsecurityrequest
        SET status          = v_status,
            approvedby      = v_approvedby,
            approvedon      = CASE WHEN v_status IN ('Approved', 'Rejected') THEN NOW() ELSE NULL END,
            rejectionreason = v_rejectionreason
        WHERE id = v_requestid;

        -- On approval: assign role via existing SP (keeps tbuserrolemaster logic in one place)
        IF v_status = 'Approved' THEN
            SELECT public.sp_api_userrolemaster(
                2,
                json_build_object(
                    'propertyid',        v_propertyid,
                    'userid',            v_userid,
                    'roleid',            v_roleid,
                    'createdby',         v_approvedby,
                    'aadharcard',        v_aadharcard,
                    'appointmentletter', v_appointmentletter
                )::jsonb
            ) INTO v_role_result;

            v_role_status := (v_role_result->>'status_code')::integer;
            -- 409 = already assigned, treat as OK
            IF v_role_status NOT IN (201, 409) THEN
                RAISE EXCEPTION 'Role assignment failed: %', v_role_result->>'message';
            END IF;
        END IF;

        RETURN json_build_object(
            'status_code', 200,
            'message', 'Security request updated successfully',
            'data', json_build_object('id', v_requestid)
        );

    ELSE
        RAISE EXCEPTION 'Invalid operation. Valid: 2 (Create), 3 (Approve/Reject)';
    END IF;

EXCEPTION WHEN OTHERS THEN
    v_error_message := SQLERRM;
    v_error_code    := SQLSTATE;
    v_json := json_build_object(
        'error_message', v_error_message,
        'error_code',    v_error_code,
        'error_source',  'sp_api_securityrequest',
        'metadata',      'Operation: ' || COALESCE(p_operation::text, '<null>') || ', JSON: ' || COALESCE(p_json::text, '<null>'),
        'created_on',    TO_CHAR(CURRENT_TIMESTAMP, 'YYYY-MM-DD HH24:MI:SS')
    );
    PERFORM sp_error_logs(2, v_json::TEXT);
    RETURN json_build_object('status_code', 500, 'message', 'An error occurred', 'error', v_error_message);
END;
$$;


-- ── 3. sp_api_getsecurityrequests ────────────────────────────────────
CREATE OR REPLACE FUNCTION public.sp_api_getsecurityrequests(
    p_status TEXT DEFAULT NULL
)
RETURNS JSON
LANGUAGE plpgsql
AS $$
DECLARE
    v_result        json;
    v_error_message text;
    v_error_code    text;
    v_json          jsonb;
BEGIN
    IF p_status IS NOT NULL AND p_status NOT IN ('Pending', 'Approved', 'Rejected') THEN
        RAISE EXCEPTION 'Invalid status. Must be Pending, Approved, or Rejected';
    END IF;

    SELECT COALESCE(json_agg(t ORDER BY t.createdon DESC), '[]'::json) INTO v_result
    FROM (
        SELECT
            r.id,
            r.propertyid,
            COALESCE(p.propertyname, '')   AS propertyname,
            r.userid,
            COALESCE(u.firstname, '')      AS userfirstname,
            COALESCE(u.lastname, '')       AS userlastname,
            COALESCE(u.email, '')          AS useremail,
            COALESCE(u.phone, '')          AS userphone,
            r.roleid,
            COALESCE(ro.rolename, '')      AS rolename,
            r.requestedby,
            COALESCE(req.firstname || ' ' || req.lastname, '') AS requestedbyname,
            COALESCE(r.status, '')         AS status,
            r.approvedby,
            COALESCE(apr.firstname || ' ' || apr.lastname, '') AS approvedbyname,
            r.approvedon,
            r.rejectionreason,
            r.createdon,
            -- Fall back to user's registration docs if request has no docs
            COALESCE(r.aadharcard,        u.aadharcard)        AS aadharcard,
            COALESCE(r.appointmentletter, u.appointmentletter) AS appointmentletter
        FROM public.tblsecurityrequest r
        LEFT JOIN public.tblpropertymaster p   ON r.propertyid   = p.id
        LEFT JOIN public.tblusermaster u        ON r.userid       = u.id
        LEFT JOIN public.tblrolemaster ro       ON r.roleid       = ro.id
        LEFT JOIN public.tblusermaster req      ON r.requestedby  = req.id
        LEFT JOIN public.tblusermaster apr      ON r.approvedby   = apr.id
        WHERE (p_status IS NULL OR r.status = p_status)
    ) t;

    RETURN json_build_object(
        'status_code', 200,
        'message', 'Security requests fetched successfully',
        'data', v_result
    );

EXCEPTION WHEN OTHERS THEN
    v_error_message := SQLERRM; v_error_code := SQLSTATE;
    v_json := json_build_object(
        'error_message', v_error_message, 'error_code', v_error_code,
        'error_source',  'sp_api_getsecurityrequests',
        'metadata',      'Status: ' || COALESCE(p_status, '<null>'),
        'created_on',    TO_CHAR(CURRENT_TIMESTAMP, 'YYYY-MM-DD HH24:MI:SS')
    );
    PERFORM sp_error_logs(2, v_json::TEXT);
    RETURN json_build_object('status_code', 500, 'message', 'An error occurred', 'error', v_error_message);
END;
$$;


-- ── 4. sp_api_getsecurityrequestbyid ─────────────────────────────────
CREATE OR REPLACE FUNCTION public.sp_api_getsecurityrequestbyid(
    p_requestid INTEGER
)
RETURNS JSON
LANGUAGE plpgsql
AS $$
DECLARE
    v_result        json;
    v_error_message text;
    v_error_code    text;
    v_json          jsonb;
BEGIN
    IF p_requestid IS NULL THEN RAISE EXCEPTION 'RequestId is required'; END IF;

    SELECT row_to_json(t) INTO v_result
    FROM (
        SELECT
            r.id,
            r.propertyid,
            COALESCE(p.propertyname, '')   AS propertyname,
            r.userid,
            COALESCE(u.firstname, '')      AS userfirstname,
            COALESCE(u.lastname, '')       AS userlastname,
            COALESCE(u.email, '')          AS useremail,
            COALESCE(u.phone, '')          AS userphone,
            r.roleid,
            COALESCE(ro.rolename, '')      AS rolename,
            r.requestedby,
            COALESCE(req.firstname || ' ' || req.lastname, '') AS requestedbyname,
            COALESCE(r.status, '')         AS status,
            r.approvedby,
            COALESCE(apr.firstname || ' ' || apr.lastname, '') AS approvedbyname,
            r.approvedon,
            r.rejectionreason,
            r.createdon,
            COALESCE(r.aadharcard,        u.aadharcard)        AS aadharcard,
            COALESCE(r.appointmentletter, u.appointmentletter) AS appointmentletter
        FROM public.tblsecurityrequest r
        LEFT JOIN public.tblpropertymaster p   ON r.propertyid   = p.id
        LEFT JOIN public.tblusermaster u        ON r.userid       = u.id
        LEFT JOIN public.tblrolemaster ro       ON r.roleid       = ro.id
        LEFT JOIN public.tblusermaster req      ON r.requestedby  = req.id
        LEFT JOIN public.tblusermaster apr      ON r.approvedby   = apr.id
        WHERE r.id = p_requestid
    ) t;

    IF v_result IS NULL THEN
        RETURN json_build_object('status_code', 404, 'message', 'Security request not found');
    END IF;

    RETURN json_build_object(
        'status_code', 200,
        'message', 'Security request fetched successfully',
        'data', v_result
    );

EXCEPTION WHEN OTHERS THEN
    v_error_message := SQLERRM; v_error_code := SQLSTATE;
    v_json := json_build_object(
        'error_message', v_error_message, 'error_code', v_error_code,
        'error_source',  'sp_api_getsecurityrequestbyid',
        'metadata',      'RequestId: ' || COALESCE(p_requestid::text, '<null>'),
        'created_on',    TO_CHAR(CURRENT_TIMESTAMP, 'YYYY-MM-DD HH24:MI:SS')
    );
    PERFORM sp_error_logs(2, v_json::TEXT);
    RETURN json_build_object('status_code', 500, 'message', 'An error occurred', 'error', v_error_message);
END;
$$;
