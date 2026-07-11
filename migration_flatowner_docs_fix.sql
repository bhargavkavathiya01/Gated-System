-- ============================================================
-- Migration: Fix flat owner document flow
--
-- Fix 1: sp_api_flatownerrequest op 2
--   When creating a request, use COALESCE so that if no docs
--   are uploaded in this call, the user's existing docs in
--   tblusermaster are NOT overwritten with NULL.
--
-- Fix 2: sp_api_getflatownerrequests
--   Fall back to user's tblusermaster docs if the request row
--   itself has no documents (covers both flows: register then
--   request, and createflatowner with docs).
--
-- Fix 3: sp_api_getflatownerrequestbyid
--   Same COALESCE fallback for the single-record fetch.
--
-- Run in pgAdmin against your PostgreSQL database.
-- ============================================================


-- ── Pre-req: add appointmentletter column to tblflatownerrequest ────
ALTER TABLE public.tblflatownerrequest
    ADD COLUMN IF NOT EXISTS appointmentletter text;


-- ── Fix 1: sp_api_flatownerrequest ──────────────────────────────────
-- Only change is the UPDATE tblusermaster block in op 2.
-- Old:  SET aadharcard = NULLIF(...), electricitybill = NULLIF(...)
-- New:  COALESCE(new_value, existing_value)  -- preserves existing docs
-- ─────────────────────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION public.sp_api_flatownerrequest(
    p_operation INTEGER,
    p_json      JSONB DEFAULT NULL
)
RETURNS JSON
LANGUAGE plpgsql
AS $$
DECLARE
    v_result          json;
    v_error_message   text;
    v_error_code      text;
    v_json            jsonb;
    v_id              integer;
    v_status          text;
    v_requestid       integer;
    v_approvedby      integer;
    v_rejectionreason text;
BEGIN
    IF p_operation = 2 THEN
        IF (p_json->>'propertyid')::integer IS NULL THEN RAISE EXCEPTION 'PropertyId is required'; END IF;
        IF (p_json->>'buildingid')::integer IS NULL THEN RAISE EXCEPTION 'BuildingId is required'; END IF;
        IF p_json->>'flatnumber' IS NULL OR p_json->>'flatnumber' = '' THEN RAISE EXCEPTION 'FlatNumber is required'; END IF;
        IF (p_json->>'userid')::integer IS NULL THEN RAISE EXCEPTION 'UserId is required'; END IF;
        IF (p_json->>'roleid')::integer IS NULL THEN RAISE EXCEPTION 'RoleId is required'; END IF;
        IF (p_json->>'requestedby')::integer IS NULL THEN RAISE EXCEPTION 'RequestedBy is required'; END IF;

        INSERT INTO public.tblflatownerrequest (
            propertyid, buildingid, flatnumber, userid, roleid,
            guesttype, requestedby, status, createdon,
            aadharcard, electricitybill, appointmentletter
        ) VALUES (
            (p_json->>'propertyid')::integer,
            (p_json->>'buildingid')::integer,
            p_json->>'flatnumber',
            (p_json->>'userid')::integer,
            (p_json->>'roleid')::integer,
            NULLIF(p_json->>'guesttype', ''),
            (p_json->>'requestedby')::integer,
            'Pending',
            NOW(),
            NULLIF(p_json->>'aadharcard', ''),
            NULLIF(p_json->>'electricitybill', ''),
            NULLIF(p_json->>'appointmentletter', '')
        ) RETURNING id INTO v_id;

        -- Update user's docs only if new docs were provided;
        -- COALESCE preserves existing docs when none are uploaded in this request.
        UPDATE public.tblusermaster
        SET aadharcard         = COALESCE(NULLIF(p_json->>'aadharcard', ''),         aadharcard),
            electricitybill    = COALESCE(NULLIF(p_json->>'electricitybill', ''),    electricitybill),
            appointmentletter  = COALESCE(NULLIF(p_json->>'appointmentletter', ''),  appointmentletter)
        WHERE id = (p_json->>'userid')::integer;

        RETURN json_build_object('status_code',201,'message','Flat owner request created successfully','data',json_build_object('id',v_id));

    ELSIF p_operation = 3 THEN
        IF (p_json->>'requestid')::integer IS NULL THEN RAISE EXCEPTION 'RequestId is required'; END IF;
        IF p_json->>'status' IS NULL OR p_json->>'status' = '' THEN RAISE EXCEPTION 'Status is required'; END IF;
        IF (p_json->>'approvedby')::integer IS NULL THEN RAISE EXCEPTION 'ApprovedBy is required'; END IF;

        v_requestid       := (p_json->>'requestid')::integer;
        v_status          := p_json->>'status';
        v_approvedby      := (p_json->>'approvedby')::integer;
        v_rejectionreason := NULLIF(p_json->>'rejectionreason', '');

        IF v_status NOT IN ('Pending','Approved','Rejected') THEN
            RAISE EXCEPTION 'Invalid status. Must be Pending, Approved, or Rejected';
        END IF;

        UPDATE public.tblflatownerrequest
        SET status          = v_status,
            approvedby      = v_approvedby,
            approvedon      = CASE WHEN v_status IN ('Approved','Rejected') THEN NOW() ELSE NULL END,
            rejectionreason = v_rejectionreason
        WHERE id = v_requestid;

        IF NOT FOUND THEN RAISE EXCEPTION 'Flat owner request not found'; END IF;

        RETURN json_build_object('status_code',200,'message','Flat owner request updated successfully','data',json_build_object('id',v_requestid));

    ELSE
        RAISE EXCEPTION 'Invalid operation. Valid operations are: 2 (Create), 3 (Update)';
    END IF;

EXCEPTION WHEN OTHERS THEN
    v_error_message := SQLERRM; v_error_code := SQLSTATE;
    v_json := json_build_object('error_message',v_error_message,'error_code',v_error_code,
        'error_source','sp_api_flatownerrequest',
        'metadata','Operation: '||COALESCE(p_operation::text,'<null>')||', JSON: '||COALESCE(p_json::text,'<null>'),
        'created_on',TO_CHAR(CURRENT_TIMESTAMP,'YYYY-MM-DD HH24:MI:SS'));
    PERFORM sp_error_logs(2, v_json::TEXT);
    RETURN json_build_object('status_code',500,'message','An error occurred','error',v_error_message);
END;
$$;


-- ── Fix 2: sp_api_getflatownerrequests ──────────────────────────────
-- COALESCE(r.aadharcard, u.aadharcard) so requests created without
-- docs still show the user's registration docs.
-- ─────────────────────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION public.sp_api_getflatownerrequests(
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
    IF p_status IS NOT NULL AND p_status NOT IN ('Pending','Approved','Rejected') THEN
        RAISE EXCEPTION 'Invalid status. Must be Pending, Approved, or Rejected';
    END IF;

    SELECT COALESCE(json_agg(t ORDER BY createdon DESC), '[]'::json) INTO v_result
    FROM (
        SELECT
            r.id,
            r.propertyid,
            COALESCE(p.propertyname, '') as propertyname,
            r.buildingid,
            COALESCE(b.buildingname, '') as buildingname,
            COALESCE(r.flatnumber, '') as flatnumber,
            r.userid,
            COALESCE(u.firstname, '') as userfirstname,
            COALESCE(u.lastname, '') as userlastname,
            COALESCE(u.email, '') as useremail,
            COALESCE(u.phone, '') as userphone,
            r.roleid,
            COALESCE(ro.rolename, '') as rolename,
            r.guesttype,
            r.requestedby,
            COALESCE(requester.firstname || ' ' || requester.lastname, '') as requestedbyname,
            COALESCE(r.status, '') as status,
            r.approvedby,
            COALESCE(approver.firstname || ' ' || approver.lastname, '') as approvedbyname,
            r.approvedon,
            r.rejectionreason,
            r.createdon,
            -- Fall back to user's registration docs if request has no docs
            COALESCE(r.aadharcard,      u.aadharcard)      as aadharcard,
            COALESCE(r.electricitybill, u.electricitybill) as electricitybill,
            COALESCE(r.appointmentletter, u.appointmentletter) as appointmentletter
        FROM public.tblflatownerrequest r
        LEFT JOIN public.tblpropertymaster p ON r.propertyid = p.id
        LEFT JOIN public.tblbuildingmaster b ON r.buildingid = b.id
        LEFT JOIN public.tblusermaster u ON r.userid = u.id
        LEFT JOIN public.tblrolemaster ro ON r.roleid = ro.id
        LEFT JOIN public.tblusermaster requester ON r.requestedby = requester.id
        LEFT JOIN public.tblusermaster approver ON r.approvedby = approver.id
        WHERE (p_status IS NULL OR r.status = p_status)
    ) t;

    RETURN json_build_object('status_code',200,'message','Flat owner requests fetched successfully','data',v_result);

EXCEPTION WHEN OTHERS THEN
    v_error_message := SQLERRM; v_error_code := SQLSTATE;
    v_json := json_build_object('error_message',v_error_message,'error_code',v_error_code,
        'error_source','sp_api_getflatownerrequests',
        'metadata','Status: '||COALESCE(p_status,'<null>'),
        'created_on',TO_CHAR(CURRENT_TIMESTAMP,'YYYY-MM-DD HH24:MI:SS'));
    PERFORM sp_error_logs(2, v_json::TEXT);
    RETURN json_build_object('status_code',500,'message','An error occurred','error',v_error_message);
END;
$$;


-- ── Fix 3: sp_api_getflatownerrequestbyid ───────────────────────────
-- Same COALESCE fallback for single-record fetch.
-- Rewrite the SP to include doc fallback; preserves existing logic.
-- ─────────────────────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION public.sp_api_getflatownerrequestbyid(
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
    IF p_requestid IS NULL THEN
        RAISE EXCEPTION 'RequestId is required';
    END IF;

    SELECT row_to_json(t) INTO v_result
    FROM (
        SELECT
            r.id,
            r.propertyid,
            COALESCE(p.propertyname, '') as propertyname,
            r.buildingid,
            COALESCE(b.buildingname, '') as buildingname,
            COALESCE(r.flatnumber, '') as flatnumber,
            r.userid,
            COALESCE(u.firstname, '') as userfirstname,
            COALESCE(u.lastname, '') as userlastname,
            COALESCE(u.email, '') as useremail,
            COALESCE(u.phone, '') as userphone,
            r.roleid,
            COALESCE(ro.rolename, '') as rolename,
            r.guesttype,
            r.requestedby,
            COALESCE(requester.firstname || ' ' || requester.lastname, '') as requestedbyname,
            COALESCE(r.status, '') as status,
            r.approvedby,
            COALESCE(approver.firstname || ' ' || approver.lastname, '') as approvedbyname,
            r.approvedon,
            r.rejectionreason,
            r.createdon,
            COALESCE(r.aadharcard,      u.aadharcard)      as aadharcard,
            COALESCE(r.electricitybill, u.electricitybill) as electricitybill,
            COALESCE(r.appointmentletter, u.appointmentletter) as appointmentletter
        FROM public.tblflatownerrequest r
        LEFT JOIN public.tblpropertymaster p ON r.propertyid = p.id
        LEFT JOIN public.tblbuildingmaster b ON r.buildingid = b.id
        LEFT JOIN public.tblusermaster u ON r.userid = u.id
        LEFT JOIN public.tblrolemaster ro ON r.roleid = ro.id
        LEFT JOIN public.tblusermaster requester ON r.requestedby = requester.id
        LEFT JOIN public.tblusermaster approver ON r.approvedby = approver.id
        WHERE r.id = p_requestid
    ) t;

    IF v_result IS NULL THEN
        RETURN json_build_object('status_code',404,'message','Flat owner request not found');
    END IF;

    RETURN json_build_object('status_code',200,'message','Flat owner request fetched successfully','data',v_result);

EXCEPTION WHEN OTHERS THEN
    v_error_message := SQLERRM; v_error_code := SQLSTATE;
    v_json := json_build_object('error_message',v_error_message,'error_code',v_error_code,
        'error_source','sp_api_getflatownerrequestbyid',
        'metadata','RequestId: '||COALESCE(p_requestid::text,'<null>'),
        'created_on',TO_CHAR(CURRENT_TIMESTAMP,'YYYY-MM-DD HH24:MI:SS'));
    PERFORM sp_error_logs(2, v_json::TEXT);
    RETURN json_build_object('status_code',500,'message','An error occurred','error',v_error_message);
END;
$$;
