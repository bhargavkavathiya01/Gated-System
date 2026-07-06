-- ============================================================
-- Migration: Security Documents (AadharCard + AppointmentLetter)
--            + Buildings in GetAllProperties (builder + superadmin)
-- Run in pgAdmin against your PostgreSQL database.
-- ============================================================


-- ============================================================
-- PART A: Add appointmentletter column to tblusermaster
-- ============================================================

ALTER TABLE public.tblusermaster
    ADD COLUMN IF NOT EXISTS appointmentletter text;


-- ============================================================
-- PART A2: sp_api_usermaster — Op 2 now also stores appointmentletter
--   (aadharcard + electricitybill were already stored; add appointmentletter)
-- ============================================================

CREATE OR REPLACE FUNCTION public.sp_api_usermaster(p_operation integer, p_json text DEFAULT NULL::text)
RETURNS json LANGUAGE plpgsql AS $$
DECLARE
    v_result         JSON;
    v_userid         INT;
    v_firstname      TEXT;
    v_middlename     TEXT;
    v_lastname       TEXT;
    v_email          TEXT;
    v_phone          TEXT;
    v_password       TEXT;
    v_isactive       BOOLEAN;
    v_createdby      INT;
    v_modifiedby     INT;
    v_registertypeid INT;
    v_profile_image  TEXT;
    v_error_message  TEXT;
    v_error_code     TEXT;
    v_json           JSON;
BEGIN
    v_userid         := COALESCE((p_json::json->>'id')::INT, NULL);
    v_firstname      := p_json::json->>'firstname';
    v_middlename     := p_json::json->>'middlename';
    v_lastname       := p_json::json->>'lastname';
    v_email          := p_json::json->>'email';
    v_phone          := p_json::json->>'phone';
    v_password       := p_json::json->>'password';
    v_isactive       := COALESCE((p_json::json->>'isactive')::BOOLEAN, TRUE);
    v_createdby      := COALESCE((p_json::json->>'createdby')::INT, NULL);
    v_modifiedby     := COALESCE((p_json::json->>'modifiedby')::INT, NULL);
    v_registertypeid := COALESCE((p_json::json->>'registertypeid')::INT, NULL);
    v_profile_image  := p_json::json->>'profilepictureurl';

    -- 1 = Get all users
    IF p_operation = 1 THEN
        SELECT json_agg(t) INTO v_result
        FROM (
            SELECT
                ROW_NUMBER() OVER (ORDER BY u.firstname ASC) AS srno,
                u.id, u.firstname, u.middlename, u.lastname,
                u.email, u.phone, u.isactive,
                u.createdby, u.modifiedby, u.createdon, u.modifiedon,
                urm.roleid, rm.rolename
            FROM public.tblusermaster u
            LEFT JOIN tbuserrolemaster urm ON urm.userid = u.id
            LEFT JOIN tblrolemaster rm ON rm.id = urm.roleid
            ORDER BY u.firstname
        ) t;
        RETURN json_build_object('status_code',200,'message','Users fetched successfully','data',v_result);

    -- 2 = Insert new user
    ELSIF p_operation = 2 THEN
        IF v_firstname IS NULL OR v_lastname IS NULL OR v_email IS NULL OR v_password IS NULL THEN
            RAISE EXCEPTION 'Mandatory fields missing for insertion';
        END IF;

        IF EXISTS (SELECT 1 FROM public.tblusermaster WHERE LOWER(TRIM(email)) = LOWER(TRIM(v_email))) THEN
            RETURN json_build_object('status_code',409,'message','Email already exists');
        END IF;

        IF EXISTS (SELECT 1 FROM public.tblusermaster WHERE phone = v_phone) THEN
            RETURN json_build_object('status_code',409,'message','Phone Number already exists');
        END IF;

        INSERT INTO public.tblusermaster (
            firstname, middlename, lastname, email, phone,
            password, password_salt, isactive, createdby, createdon,
            registertypeid, profileimage,
            aadharcard, electricitybill, appointmentletter
        )
        VALUES (
            v_firstname, v_middlename, v_lastname, v_email, v_phone,
            crypt(v_password, gen_salt('md5')), NULL,
            v_isactive, v_createdby, CURRENT_TIMESTAMP,
            v_registertypeid, v_profile_image,
            NULLIF(p_json::json->>'aadharcard', ''),
            NULLIF(p_json::json->>'electricitybill', ''),
            NULLIF(p_json::json->>'appointmentletter', '')
        )
        RETURNING id INTO v_userid;

        RETURN json_build_object('status_code',201,'message','User inserted successfully','data',json_build_object('id',v_userid));

    -- 3 = Update user
    ELSIF p_operation = 3 THEN
        IF v_userid IS NULL OR v_modifiedby IS NULL THEN
            RAISE EXCEPTION 'Mandatory fields missing for update';
        END IF;

        IF v_email IS NOT NULL AND EXISTS (
            SELECT 1 FROM public.tblusermaster
            WHERE LOWER(TRIM(email)) = LOWER(TRIM(v_email)) AND id <> v_userid
        ) THEN
            RETURN json_build_object('status_code',409,'message','Email already exists');
        END IF;

        IF v_phone IS NOT NULL AND EXISTS (
            SELECT 1 FROM public.tblusermaster
            WHERE LOWER(TRIM(phone)) = LOWER(TRIM(v_phone)) AND id <> v_userid
        ) THEN
            RETURN json_build_object('status_code',409,'message','Phone number already exists');
        END IF;

        UPDATE public.tblusermaster
        SET firstname   = COALESCE(v_firstname, firstname),
            middlename  = COALESCE(v_middlename, middlename),
            lastname    = COALESCE(v_lastname, lastname),
            email       = COALESCE(v_email, email),
            phone       = COALESCE(v_phone, phone),
            profileimage= COALESCE(v_profile_image, profileimage),
            modifiedby  = v_modifiedby,
            modifiedon  = CURRENT_TIMESTAMP,
            password    = CASE
                WHEN v_password IS NOT NULL AND v_password <> ''
                THEN crypt(v_password, gen_salt('md5'))
                ELSE password
            END
        WHERE id = v_userid;

        RETURN json_build_object('status_code',200,'message','User updated successfully');

    -- 4 = Delete user
    ELSIF p_operation = 4 THEN
        IF v_userid IS NULL THEN RAISE EXCEPTION 'Mandatory field id missing for deletion'; END IF;
        DELETE FROM public.tblusermaster WHERE id = v_userid;
        RETURN json_build_object('status_code',200,'message','User deleted successfully');

    -- 5 = Get by ID
    ELSIF p_operation = 5 THEN
        IF v_userid IS NULL THEN RAISE EXCEPTION 'Mandatory field id missing for fetching by ID'; END IF;
        SELECT json_agg(t) INTO v_result
        FROM (
            SELECT id AS userid, firstname, middlename, lastname, email
            FROM public.tblusermaster WHERE id = v_userid
        ) t;
        RETURN json_build_object('status_code',200,'message','User fetched successfully','data',v_result);

    ELSE
        RETURN json_build_object('status_code',400,'message','Invalid operation');
    END IF;

EXCEPTION WHEN OTHERS THEN
    v_error_message := SQLERRM;
    v_error_code    := SQLSTATE;
    v_json := json_build_object(
        'error_message',v_error_message,'error_code',v_error_code,
        'error_source','sp_api_usermaster',
        'metadata','Operation: '||p_operation||', Data: '||p_json::text,
        'created_on',TO_CHAR(CURRENT_TIMESTAMP,'YYYY-MM-DD HH24:MI:SS')
    );
    PERFORM sp_error_logs(2, v_json::TEXT);
    RETURN json_build_object('status_code',500,'message','An error occurred','error',v_error_message);
END; $$;


-- ============================================================
-- PART B: sp_api_userrolemaster
--   Op 2 (insert): now accepts aadharcard + appointmentletter
--   and writes them onto the user record in tblusermaster so
--   they are returned by sp_api_authenticateuser on login.
-- ============================================================

CREATE OR REPLACE FUNCTION public.sp_api_userrolemaster(p_operation integer, p_json text DEFAULT NULL::text)
RETURNS json LANGUAGE plpgsql AS $$
DECLARE
    v_result          json;
    v_id              int;
    v_userid          int;
    v_propertyid      int;
    v_buildingid      int;
    v_flatnumber      text;
    v_roleid          int;
    v_createdby       int;
    v_aadharcard      text;
    v_appointmentletter text;
    v_error_message   text;
    v_error_code      text;
    v_log_json        json;
BEGIN
    IF p_json IS NOT NULL THEN
        v_userid          := NULLIF(p_json::json->>'userid','')::int;
        v_propertyid      := NULLIF(p_json::json->>'propertyid','')::int;
        v_buildingid      := NULLIF(p_json::json->>'buildingid','')::int;
        v_flatnumber      := NULLIF(trim(p_json::json->>'flatnumber'),'');
        v_roleid          := NULLIF(p_json::json->>'roleid','')::int;
        v_createdby       := NULLIF(p_json::json->>'createdby','')::int;
        v_aadharcard      := NULLIF(trim(p_json::json->>'aadharcard'),'');
        v_appointmentletter := NULLIF(trim(p_json::json->>'appointmentletter'),'');
    END IF;

    -- 1 = Get all role assignments
    IF p_operation = 1 THEN
        SELECT json_agg(t) INTO v_result
        FROM (
            SELECT urm.id, urm.userid, urm.propertyid, urm.buildingid,
                   urm.flatnumber, urm.roleid, rm.rolename,
                   urm.createdby, urm.createdon
            FROM public.tbuserrolemaster urm
            LEFT JOIN public.tblrolemaster rm ON rm.id = urm.roleid
            ORDER BY urm.createdon DESC
        ) t;
        RETURN json_build_object('status_code',200,'message','Role assignments fetched','data',v_result);

    -- 2 = Insert new role assignment
    ELSIF p_operation = 2 THEN
        IF v_userid IS NULL THEN
            RETURN json_build_object('status_code',400,'message','userId is required');
        END IF;
        IF v_roleid IS NULL THEN
            RETURN json_build_object('status_code',400,'message','roleId is required');
        END IF;

        -- Prevent duplicate role assignment for same user + property + role
        IF EXISTS (
            SELECT 1 FROM public.tbuserrolemaster
            WHERE userid = v_userid
              AND roleid = v_roleid
              AND COALESCE(propertyid,0) = COALESCE(v_propertyid,0)
        ) THEN
            RETURN json_build_object('status_code',409,'message','User already has this role for the given property');
        END IF;

        INSERT INTO public.tbuserrolemaster (userid, propertyid, buildingid, flatnumber, roleid, createdby, createdon)
        VALUES (v_userid, v_propertyid, v_buildingid, v_flatnumber, v_roleid, v_createdby, CURRENT_TIMESTAMP)
        RETURNING id INTO v_id;

        -- Persist documents onto user master so login returns them
        IF v_aadharcard IS NOT NULL OR v_appointmentletter IS NOT NULL THEN
            UPDATE public.tblusermaster
            SET aadharcard        = COALESCE(v_aadharcard, aadharcard),
                appointmentletter = COALESCE(v_appointmentletter, appointmentletter)
            WHERE id = v_userid;
        END IF;

        RETURN json_build_object('status_code',201,'message','Role assignment created','data',json_build_object('id',v_id));

    -- 3 = Update role assignment
    ELSIF p_operation = 3 THEN
        v_id := NULLIF(p_json::json->>'id','')::int;
        IF v_id IS NULL THEN
            RETURN json_build_object('status_code',400,'message','id is required for update');
        END IF;

        UPDATE public.tbuserrolemaster
        SET propertyid   = COALESCE(v_propertyid, propertyid),
            buildingid   = COALESCE(v_buildingid, buildingid),
            flatnumber   = COALESCE(v_flatnumber, flatnumber),
            roleid       = COALESCE(v_roleid, roleid)
        WHERE id = v_id;

        RETURN json_build_object('status_code',200,'message','Role assignment updated');

    -- 4 = Delete role assignment
    ELSIF p_operation = 4 THEN
        v_id := NULLIF(p_json::json->>'id','')::int;
        IF v_id IS NULL THEN
            RETURN json_build_object('status_code',400,'message','id is required for delete');
        END IF;

        DELETE FROM public.tbuserrolemaster WHERE id = v_id;
        RETURN json_build_object('status_code',200,'message','Role assignment deleted');

    ELSE
        RETURN json_build_object('status_code',400,'message','Invalid operation');
    END IF;

EXCEPTION WHEN OTHERS THEN
    v_error_message := SQLERRM;
    v_error_code    := SQLSTATE;
    v_log_json := json_build_object(
        'error_message',v_error_message,'error_code',v_error_code,
        'error_source','sp_api_userrolemaster',
        'metadata','Operation: '||p_operation||', Data: '||COALESCE(p_json,'')::text,
        'created_on',TO_CHAR(CURRENT_TIMESTAMP,'YYYY-MM-DD HH24:MI:SS')
    );
    PERFORM sp_error_logs(2, v_log_json::text);
    RETURN json_build_object('status_code',500,'message','An error occurred','error',v_error_message);
END; $$;


-- ============================================================
-- PART C: sp_api_authenticateuser
--   Add appointmentLetter to the login response (from tblusermaster).
--   aadharCard and electricityBill were already there; we add appointmentLetter.
-- ============================================================

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
               'userid',            u.id,
               'firstname',         u.firstname,
               'middlename',        u.middlename,
               'lastname',          u.lastname,
               'email',             u.email,
               'phone',             u.phone,
               'permanentQR', (
                   SELECT qrcode FROM public.tblvisitorrequest
                   WHERE flatownerid = u.id ORDER BY createdon DESC LIMIT 1
               ),
               'userType',          u.registertypeid,
               'profileImage',      u.profileimage,
               'aadharCard',        u.aadharcard,
               'electricityBill',   u.electricitybill,
               'appointmentLetter', u.appointmentletter
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


-- ============================================================
-- PART D: sp_api_propertymaster
--   Op 5 (get by builder): include a buildings JSON array per property.
--   All other operations are preserved unchanged.
-- ============================================================

CREATE OR REPLACE FUNCTION public.sp_api_propertymaster(p_operation integer, p_json text DEFAULT NULL::text)
RETURNS json LANGUAGE plpgsql AS $$
DECLARE
    v_result          json;
    v_id              int;
    v_propertyname    text;
    v_address         text;
    v_city            text;
    v_pincode         text;
    v_builderid       int;
    v_isverified      text;
    v_regcert         text;
    v_pancard         text;
    v_tancard         text;
    v_error_message   text;
    v_error_code      text;
    v_log_json        json;
BEGIN
    IF p_json IS NOT NULL THEN
        v_id           := NULLIF(p_json::json->>'id','')::int;
        v_propertyname := NULLIF(trim(p_json::json->>'propertyname'),'');
        v_address      := NULLIF(trim(p_json::json->>'address'),'');
        v_city         := NULLIF(trim(p_json::json->>'city'),'');
        v_pincode      := NULLIF(trim(p_json::json->>'pincode'),'');
        v_builderid    := NULLIF(p_json::json->>'builderid','')::int;
        v_isverified   := COALESCE(NULLIF(trim(p_json::json->>'isverified'),''),'Pending');
        v_regcert      := NULLIF(trim(p_json::json->>'registrationcertificate'),'');
        v_pancard      := NULLIF(trim(p_json::json->>'pancard'),'');
        v_tancard      := NULLIF(trim(p_json::json->>'tancard'),'');
    END IF;

    -- 1 = Get all properties
    IF p_operation = 1 THEN
        SELECT json_agg(t) INTO v_result
        FROM (
            SELECT id, propertyname, address, city, pincode, builderid,
                   isverified, createdon, modifiedon,
                   registrationcertificate, pancard, tancard
            FROM public.tblpropertymaster
            ORDER BY propertyname
        ) t;
        RETURN json_build_object('status_code',200,'message','Properties fetched','data',v_result);

    -- 2 = Insert new property
    ELSIF p_operation = 2 THEN
        IF v_propertyname IS NULL OR v_builderid IS NULL THEN
            RETURN json_build_object('status_code',400,'message','propertyname and builderid are required');
        END IF;

        INSERT INTO public.tblpropertymaster (
            propertyname, address, city, pincode, builderid,
            isverified, registrationcertificate, pancard, tancard, createdon
        ) VALUES (
            v_propertyname, v_address, v_city, v_pincode, v_builderid,
            'Pending', v_regcert, v_pancard, v_tancard, CURRENT_TIMESTAMP
        ) RETURNING id INTO v_id;

        RETURN json_build_object('status_code',201,'message','Property created','data',json_build_object('id',v_id));

    -- 3 = Update property
    ELSIF p_operation = 3 THEN
        IF v_id IS NULL THEN
            RETURN json_build_object('status_code',400,'message','id is required for update');
        END IF;

        UPDATE public.tblpropertymaster
        SET propertyname             = COALESCE(v_propertyname, propertyname),
            address                  = COALESCE(v_address, address),
            city                     = COALESCE(v_city, city),
            pincode                  = COALESCE(v_pincode, pincode),
            isverified               = COALESCE(v_isverified, isverified),
            registrationcertificate  = COALESCE(v_regcert, registrationcertificate),
            pancard                  = COALESCE(v_pancard, pancard),
            tancard                  = COALESCE(v_tancard, tancard),
            modifiedon               = CURRENT_TIMESTAMP
        WHERE id = v_id;

        RETURN json_build_object('status_code',200,'message','Property updated');

    -- 5 = Get properties by builder — includes buildings per property
    ELSIF p_operation = 5 THEN
        IF v_builderid IS NULL THEN
            RETURN json_build_object('status_code',400,'message','builderid is required');
        END IF;

        SELECT json_agg(prop_row) INTO v_result
        FROM (
            SELECT
                pm.id,
                pm.propertyname,
                pm.address,
                pm.city,
                pm.pincode,
                pm.builderid,
                pm.isverified,
                pm.createdon,
                (
                    SELECT COALESCE(json_agg(json_build_object('id', bm.id, 'buildingname', bm.buildingname) ORDER BY bm.buildingname), '[]'::json)
                    FROM public.tblbuildingmaster bm
                    WHERE bm.propertyid = pm.id
                ) AS buildings
            FROM public.tblpropertymaster pm
            WHERE pm.builderid = v_builderid
            ORDER BY pm.propertyname
        ) prop_row;

        IF v_result IS NULL THEN
            v_result := '[]'::json;
        END IF;

        RETURN json_build_object('status_code',200,'message','Properties fetched','data',v_result);

    ELSE
        RETURN json_build_object('status_code',400,'message','Invalid operation');
    END IF;

EXCEPTION WHEN OTHERS THEN
    v_error_message := SQLERRM;
    v_error_code    := SQLSTATE;
    v_log_json := json_build_object(
        'error_message',v_error_message,'error_code',v_error_code,
        'error_source','sp_api_propertymaster',
        'metadata','Operation: '||p_operation||', Data: '||COALESCE(p_json,'')::text,
        'created_on',TO_CHAR(CURRENT_TIMESTAMP,'YYYY-MM-DD HH24:MI:SS')
    );
    PERFORM sp_error_logs(2, v_log_json::text);
    RETURN json_build_object('status_code',500,'message','An error occurred','error',v_error_message);
END; $$;


-- ============================================================
-- PART E: sp_api_superadminpropertymaster
--   Op 1 (get all): include buildings array per property.
--   Op 5 (get by verification status): include buildings array per property.
-- ============================================================

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
                   pm.verifiedby, CONCAT(vu.firstname,' ',vu.lastname) as verifiedbyname,
                   (
                       SELECT COALESCE(json_agg(json_build_object('id', bm.id, 'buildingname', bm.buildingname) ORDER BY bm.buildingname), '[]'::json)
                       FROM public.tblbuildingmaster bm
                       WHERE bm.propertyid = pm.id
                   ) AS buildings
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
                   pm.verifiedby, CONCAT(vu.firstname,' ',vu.lastname) as verifiedbyname,
                   (
                       SELECT COALESCE(json_agg(json_build_object('id', bm.id, 'buildingname', bm.buildingname) ORDER BY bm.buildingname), '[]'::json)
                       FROM public.tblbuildingmaster bm
                       WHERE bm.propertyid = pm.id
                   ) AS buildings
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
