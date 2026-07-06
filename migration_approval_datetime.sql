-- ============================================================
-- Migration: Approval datetime tracking + AadharCard/ElectricityBill docs
-- Run ALL statements in pgAdmin against your PostgreSQL database.
-- ============================================================


-- ============================================================
-- PART A: Approval datetime SP fixes (visitor + property)
-- ============================================================

-- FIX 1: sp_api_visitorrequest
--   • Op 3 second UPDATE branch — add approvedby + approvedon
--   • Op 1 SELECT — add approvedby + approvedon

CREATE OR REPLACE FUNCTION public.sp_api_visitorrequest(p_operation integer, p_json text DEFAULT NULL::text)
RETURNS json LANGUAGE plpgsql AS $$
DECLARE
    v_result        json;
    v_id            int;
    v_visitorname   text;
    v_phone         text;
    v_purpose       text;
    v_propertyid    int;
    v_buildingid    int;
    v_flatid        int;
    v_requestedby   int;
    v_qrcode        text;
    v_expiry        timestamptz;
    v_status        text;
    v_qr_type       text;
    v_max_uses      int;
    v_used_count    int;
    v_createdon     timestamptz;
    v_error_message text;
    v_error_code    text;
    v_json          json;
    v_flatOwnerId   INT;
    v_row           RECORD;
BEGIN
    IF p_json IS NOT NULL THEN
        v_id          := NULLIF(p_json::json->>'id','')::int;
        v_visitorname := NULLIF(trim(p_json::json->>'visitorname'), '');
        v_phone       := NULLIF(trim(p_json::json->>'phone'), '');
        v_purpose     := NULLIF(trim(p_json::json->>'purpose'), '');
        v_propertyid  := NULLIF(p_json::json->>'propertyid','')::int;
        v_buildingid  := NULLIF(p_json::json->>'buildingid','')::int;
        v_flatid      := NULLIF(p_json::json->>'flatid','')::int;
        v_requestedby := NULLIF(p_json::json->>'requestedby','')::int;
        v_flatOwnerId := NULLIF(p_json::json->>'flatownerid','')::int;
        v_qrcode      := NULLIF(trim(p_json::json->>'qrcode'), '');
        IF p_json::jsonb ? 'expiry' THEN v_expiry := (p_json::jsonb->>'expiry')::timestamptz; ELSE v_expiry := NULL; END IF;
        v_status  := COALESCE(nullif(trim(p_json::json->>'status'), ''), 'Active');
        v_qr_type := COALESCE(nullif(lower(trim(p_json::json->>'qr_type')), ''), 'time');
        IF p_json::jsonb ? 'max_uses' THEN v_max_uses := (p_json::json->>'max_uses')::int; ELSE v_max_uses := NULL; END IF;
        IF p_json::jsonb ? 'used_count' THEN v_used_count := COALESCE((p_json::json->>'used_count')::int, 0); ELSE v_used_count := NULL; END IF;
    END IF;

    IF p_operation = 1 THEN
        SELECT json_agg(t) INTO v_result
        FROM (
            SELECT id, visitorname, phone, purpose, propertyid, buildingid, flatid,
                   requestedby, qrcode, expiry, qr_type, max_uses, used_count,
                   status, createdon, approvedby, approvedon
            FROM tblvisitorrequest ORDER BY createdon DESC
        ) t;
        RETURN json_build_object('status_code',200,'message','Visitor requests fetched','data',v_result);

    ELSIF p_operation = 2 THEN
        IF v_visitorname IS NULL OR v_propertyid IS NULL OR v_flatid IS NULL OR v_qrcode IS NULL THEN
            RETURN json_build_object('status_code',400,'message','visitorname, propertyid, flatid and qrcode required');
        END IF;
        v_qr_type := COALESCE(v_qr_type, 'time');
        IF v_qr_type NOT IN ('time','one_time','multi','unlimited','manual') THEN
            RETURN json_build_object('status_code',400,'message','qr_type must be one of time, one_time, multi, unlimited, manual');
        END IF;
        IF v_qr_type = 'time' THEN
            IF v_expiry IS NULL THEN RETURN json_build_object('status_code',400,'message','expiry required for qr_type=time'); END IF;
        ELSIF v_qr_type = 'one_time' THEN
            IF v_max_uses IS NULL THEN v_max_uses := 1; ELSIF v_max_uses <= 0 THEN v_max_uses := 1; END IF;
        ELSIF v_qr_type = 'multi' THEN
            IF v_max_uses IS NULL OR v_max_uses <= 0 THEN RETURN json_build_object('status_code',400,'message','max_uses (>0) required for qr_type=multi'); END IF;
        ELSIF v_qr_type = 'unlimited' THEN v_max_uses := NULL;
        END IF;
        v_used_count := 0;
        INSERT INTO tblvisitorrequest (visitorname, phone, purpose, propertyid, buildingid, flatid,
            requestedby, qrcode, expiry, qr_type, max_uses, used_count, status, createdon, flatownerid)
        VALUES (v_visitorname, v_phone, v_purpose, v_propertyid, v_buildingid, v_flatid,
            v_requestedby, v_qrcode, v_expiry, v_qr_type, v_max_uses, v_used_count, v_status, now(), v_flatOwnerId)
        RETURNING id INTO v_id;
        RETURN json_build_object('status_code',201,'message','Visitor request created','data',json_build_object('id',v_id));

    ELSIF p_operation = 3 THEN
        IF v_id IS NULL THEN RETURN json_build_object('status_code',400,'message','id required for update'); END IF;
        IF v_qr_type IS NOT NULL AND v_qr_type NOT IN ('time','one_time','multi','unlimited','manual') THEN
            RETURN json_build_object('status_code',400,'message','qr_type must be one of time, one_time, multi, unlimited, manual');
        END IF;
        IF v_qr_type = 'multi' AND v_max_uses IS NOT NULL AND v_max_uses <= 0 THEN
            RETURN json_build_object('status_code',400,'message','max_uses must be > 0 for qr_type=multi');
        END IF;
        IF v_used_count IS NOT NULL THEN
            UPDATE tblvisitorrequest
            SET visitorname = COALESCE(v_visitorname, visitorname), phone = COALESCE(v_phone, phone),
                purpose = COALESCE(v_purpose, purpose), propertyid = COALESCE(v_propertyid, propertyid),
                buildingid = COALESCE(v_buildingid, buildingid), flatid = COALESCE(v_flatid, flatid),
                requestedby = COALESCE(v_requestedby, requestedby), qrcode = COALESCE(v_qrcode, qrcode),
                expiry = COALESCE(v_expiry, expiry), qr_type = COALESCE(v_qr_type, qr_type),
                max_uses = COALESCE(v_max_uses, max_uses), used_count = v_used_count,
                status = COALESCE(v_status, status), modifiedon = now(),
                approvedby = (p_json::json->>'approvedby')::integer, approvedon = NOW()
            WHERE id = v_id;
        ELSE
            UPDATE tblvisitorrequest
            SET visitorname = COALESCE(v_visitorname, visitorname), phone = COALESCE(v_phone, phone),
                purpose = COALESCE(v_purpose, purpose), propertyid = COALESCE(v_propertyid, propertyid),
                buildingid = COALESCE(v_buildingid, buildingid), flatid = COALESCE(v_flatid, flatid),
                requestedby = COALESCE(v_requestedby, requestedby), qrcode = COALESCE(v_qrcode, qrcode),
                expiry = COALESCE(v_expiry, expiry), qr_type = COALESCE(v_qr_type, qr_type),
                max_uses = COALESCE(v_max_uses, max_uses), status = COALESCE(v_status, status),
                modifiedon = now(),
                approvedby = (p_json::json->>'approvedby')::integer, approvedon = NOW()
            WHERE id = v_id;
        END IF;
        RETURN json_build_object('status_code',200,'message','Visitor request updated');

    ELSIF p_operation = 4 THEN
        IF v_id IS NULL THEN RETURN json_build_object('status_code',400,'message','id required for delete'); END IF;
        DELETE FROM tblvisitorrequest WHERE id = v_id;
        RETURN json_build_object('status_code',200,'message','Visitor request deleted');

    ELSIF p_operation = 5 THEN
        IF v_id IS NULL THEN RETURN json_build_object('status_code',400,'message','id required'); END IF;
        SELECT * INTO v_row FROM tblvisitorrequest WHERE id = v_id FOR UPDATE;
        IF NOT FOUND THEN RETURN json_build_object('status_code',404,'message','Visitor request not found'); END IF;
        IF COALESCE(upper(v_row.status),'') <> 'REVOKED' THEN
            IF v_row.expiry IS NOT NULL AND v_row.expiry < now() THEN
                UPDATE tblvisitorrequest SET status = 'Expired', modifiedon = now() WHERE id = v_id;
            ELSIF v_row.max_uses IS NOT NULL AND COALESCE(v_row.used_count,0) >= v_row.max_uses THEN
                UPDATE tblvisitorrequest SET status = 'Expired', modifiedon = now() WHERE id = v_id;
            END IF;
        END IF;
        SELECT json_agg(t) INTO v_result
        FROM (
            SELECT id, visitorname, phone, purpose, propertyid, buildingid, flatid,
                   requestedby, qrcode, expiry, qr_type, max_uses, used_count,
                   status, createdon, approvedby, approvedon
            FROM tblvisitorrequest WHERE id = v_id
        ) t;
        RETURN json_build_object('status_code',200,'message','Visitor request fetched','data',v_result);

    ELSE
        RETURN json_build_object('status_code',400,'message','Invalid operation');
    END IF;

EXCEPTION WHEN OTHERS THEN
    v_error_message := SQLERRM; v_error_code := SQLSTATE;
    v_json := json_build_object('error_message',v_error_message,'error_code',v_error_code,
        'error_source','sp_api_visitorrequest',
        'metadata','Operation:'||p_operation||', Data:'||COALESCE(p_json,'')::text,
        'created_on',TO_CHAR(CURRENT_TIMESTAMP,'YYYY-MM-DD HH24:MI:SS'));
    PERFORM sp_error_logs(2, v_json::text);
    RETURN json_build_object('status_code',500,'message','An error occurred','error',v_error_message);
END; $$;


-- FIX 2: sp_api_superadminpropertymaster — add verifiedbyname via LEFT JOIN

CREATE OR REPLACE FUNCTION public.sp_api_superadminpropertymaster(p_operation integer, p_json text DEFAULT NULL::text)
RETURNS json LANGUAGE plpgsql AS $$
DECLARE
    v_result json; v_id int; v_propertyname text; v_address text; v_city text;
    v_pincode text; v_builderid int; v_isverified text;
    v_error_message text; v_error_code text; v_json json;
BEGIN
    IF p_json IS NOT NULL THEN
        v_id           := COALESCE((p_json::json->>'id')::int, NULL);
        v_propertyname := p_json::json->>'propertyname';
        v_address      := p_json::json->>'address';
        v_city         := p_json::json->>'city';
        v_pincode      := p_json::json->>'pincode';
        v_builderid    := COALESCE((p_json::json->>'builderid')::int, NULL);
        v_isverified   := COALESCE(p_json::json->>'isverified','Pending');
    END IF;
    IF p_operation = 1 THEN
        SELECT json_agg(t) INTO v_result FROM (
            SELECT pm.id as propertyid, pm.propertyname, pm.address, pm.city, pm.pincode,
                   pm.builderid, CONCAT(um.firstname,' ',um.lastname) as buildername,
                   pm.isverified, pm.createdon, pm.modifiedon,
                   pm.tancard, pm.pancard, pm.registrationcertificate,
                   pm.verifiedby, CONCAT(vu.firstname,' ',vu.lastname) as verifiedbyname
            FROM tblpropertymaster pm
            LEFT JOIN tblusermaster um ON pm.builderid = um.id
            LEFT JOIN tblusermaster vu ON pm.verifiedby = vu.id
            ORDER BY pm.propertyname
        ) t;
        RETURN json_build_object('status_code',200,'message','Properties fetched','data',v_result);
    ELSIF p_operation = 5 THEN
        IF v_isverified IS NULL THEN RETURN json_build_object('status_code',400,'message','Verification Status required'); END IF;
        SELECT json_agg(t) INTO v_result FROM (
            SELECT pm.id as propertyid, pm.propertyname, pm.address, pm.city, pm.pincode,
                   pm.builderid, CONCAT(um.firstname,' ',um.lastname) as buildername,
                   pm.isverified, pm.createdon, pm.modifiedon,
                   pm.verifiedby, CONCAT(vu.firstname,' ',vu.lastname) as verifiedbyname
            FROM tblpropertymaster pm
            LEFT JOIN tblusermaster um ON pm.builderid = um.id
            LEFT JOIN tblusermaster vu ON pm.verifiedby = vu.id
            WHERE pm.isverified = v_isverified ORDER BY pm.propertyname
        ) t;
        RETURN json_build_object('status_code',200,'message','Property fetched','data',v_result);
    ELSE
        RETURN json_build_object('status_code',400,'message','Invalid operation');
    END IF;
EXCEPTION WHEN OTHERS THEN
    v_error_message := SQLERRM; v_error_code := SQLSTATE;
    v_json := json_build_object('error_message',v_error_message,'error_code',v_error_code,
        'error_source','sp_api_superadminpropertymaster',
        'metadata','Operation: '||p_operation||', Data: '||COALESCE(p_json,'')::text,
        'created_on',TO_CHAR(CURRENT_TIMESTAMP,'YYYY-MM-DD HH24:MI:SS'));
    PERFORM sp_error_logs(2, v_json::text);
    RETURN json_build_object('status_code',500,'message','An error occurred','error',v_error_message);
END; $$;


-- ============================================================
-- PART B: AadharCard + ElectricityBill document support
-- ============================================================

-- Step 1: Add columns
ALTER TABLE public.tblflatownerrequest
    ADD COLUMN IF NOT EXISTS aadharcard text,
    ADD COLUMN IF NOT EXISTS electricitybill text;

ALTER TABLE public.tblusermaster
    ADD COLUMN IF NOT EXISTS aadharcard text,
    ADD COLUMN IF NOT EXISTS electricitybill text;


-- Step 2: sp_api_flatownerrequest
--   Op 2: store docs in tblflatownerrequest + update tblusermaster for login response

CREATE OR REPLACE FUNCTION public.sp_api_flatownerrequest(p_operation integer, p_json jsonb)
RETURNS json LANGUAGE plpgsql AS $$
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
            guesttype, requestedby, status, createdon, aadharcard, electricitybill
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
            NULLIF(p_json->>'electricitybill', '')
        ) RETURNING id INTO v_id;

        -- Store docs on user record so they appear in login response
        UPDATE public.tblusermaster
        SET aadharcard      = NULLIF(p_json->>'aadharcard', ''),
            electricitybill = NULLIF(p_json->>'electricitybill', '')
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
END; $$;


-- Step 3: sp_api_getflatownerrequests — add aadharcard + electricitybill to SELECT

CREATE OR REPLACE FUNCTION public.sp_api_getflatownerrequests(p_status text DEFAULT NULL::text)
RETURNS json LANGUAGE plpgsql AS $$
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
            r.aadharcard,
            r.electricitybill
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
END; $$;


-- Step 4: sp_api_getflatownerrequestbyid — add aadharcard + electricitybill to SELECT

CREATE OR REPLACE FUNCTION public.sp_api_getflatownerrequestbyid(p_requestid integer)
RETURNS json LANGUAGE plpgsql AS $$
DECLARE
    v_result        json;
    v_error_message text;
    v_error_code    text;
    v_json          jsonb;
BEGIN
    IF p_requestid IS NULL THEN RAISE EXCEPTION 'RequestId (p_requestid) is required'; END IF;

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
            r.aadharcard,
            r.electricitybill
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
        RETURN json_build_object('status_code',404,'message','Flat owner request not found','data',NULL);
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
END; $$;


-- Step 5: sp_api_authenticateuser — add aadharcard + electricitybill to login response

CREATE OR REPLACE FUNCTION public.sp_api_authenticateuser(p_json text DEFAULT NULL::text)
RETURNS json LANGUAGE plpgsql AS $$
DECLARE
    v_user          text;
    v_password      text;
    v_result        json;
    v_error_message text;
    v_error_code    text;
    v_log_json      json;
BEGIN
    IF p_json IS NULL OR trim(p_json) = '' THEN
        RETURN json_build_object('status_code',400,'message','Request JSON is required');
    END IF;

    v_user     := p_json::json->>'user';
    v_password := p_json::json->>'password';

    IF v_user IS NULL OR trim(v_user) = '' OR v_password IS NULL OR trim(v_password) = '' THEN
        RETURN json_build_object('status_code',400,'message','User and password are required');
    END IF;

    SELECT json_build_object(
               'userid',         u.id,
               'firstname',      u.firstname,
               'middlename',     u.middlename,
               'lastname',       u.lastname,
               'email',          u.email,
               'phone',          u.phone,
               'permanentQR', (
                   SELECT qrcode FROM public.tblvisitorrequest
                   WHERE flatownerid = u.id ORDER BY createdon DESC LIMIT 1
               ),
               'userType',       u.registertypeid,
               'profileImage',   u.profileimage,
               'aadharCard',     u.aadharcard,
               'electricityBill',u.electricitybill
           )
    INTO v_result
    FROM public.tblusermaster u
    WHERE (lower(trim(u.phone)) = lower(v_user) OR lower(trim(u.email)) = lower(v_user))
      AND u.password = crypt(v_password, u.password)
      AND u.isactive = TRUE
    LIMIT 1;

    IF v_result IS NULL THEN
        RETURN json_build_object('status_code',401,'message','Invalid User or password','data',NULL);
    END IF;

    RETURN json_build_object('status_code',200,'message','User authenticated successfully','data',v_result);

EXCEPTION WHEN OTHERS THEN
    v_error_message := SQLERRM; v_error_code := SQLSTATE;
    v_log_json := json_build_object('error_message',v_error_message,'error_code',v_error_code,
        'error_source','sp_api_authenticateuser',
        'metadata','Data: '||COALESCE(p_json,'')::text,
        'created_on',TO_CHAR(CURRENT_TIMESTAMP,'YYYY-MM-DD HH24:MI:SS'));
    PERFORM sp_error_logs(2, v_log_json::text);
    RETURN json_build_object('status_code',500,'message','An error occurred','error',v_error_message);
END; $$;
