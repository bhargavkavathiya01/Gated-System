-- Adds two soft-delete (deactivate) operations to sp_api_usermaster.
-- Op 6 (admin/builder path, called with a valid JWT): accepts either {"id": <int>}
-- or {"user": "<email or phone>"} to resolve the target account.
-- Op 7 (self-service, no auth): accepts {"email": "...", "phone": "..."} and only
-- deletes the account if BOTH values match the same row.
-- Both set isactive = false (already enforced by sp_api_authenticateuser at login),
-- and clean up device tokens / refresh tokens so the account is fully logged out.
-- No rows are hard-deleted from tblusermaster; existing FK-referencing data
-- (chat messages, complaints, properties, approvals, etc.) is left untouched.

CREATE OR REPLACE FUNCTION public.sp_api_usermaster(p_operation integer, p_json text DEFAULT NULL::text)
 RETURNS json
 LANGUAGE plpgsql
AS $function$
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
    v_delete_user    TEXT;
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
    v_delete_user    := p_json::json->>'user';

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

    -- 4 = Delete user (hard delete, unused by the API today; kept for compatibility)
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

    -- 6 = Soft delete (deactivate) account by id OR by email/phone
    ELSIF p_operation = 6 THEN
        IF v_userid IS NULL THEN
            IF v_delete_user IS NULL OR trim(v_delete_user) = '' THEN
                RAISE EXCEPTION 'Either id or user (email/phone) is required for deletion';
            END IF;

            SELECT id INTO v_userid
            FROM public.tblusermaster
            WHERE lower(trim(email)) = lower(trim(v_delete_user))
               OR lower(trim(phone)) = lower(trim(v_delete_user))
            LIMIT 1;
        END IF;

        IF v_userid IS NULL THEN
            RETURN json_build_object('status_code',404,'message','User not found');
        END IF;

        IF NOT EXISTS (SELECT 1 FROM public.tblusermaster WHERE id = v_userid AND isactive = TRUE) THEN
            RETURN json_build_object('status_code',409,'message','User account is already deleted');
        END IF;

        UPDATE public.tblusermaster
        SET isactive   = FALSE,
            modifiedby = COALESCE(v_modifiedby, v_userid),
            modifiedon = CURRENT_TIMESTAMP
        WHERE id = v_userid;

        -- Stop this account from being targeted by push notifications
        UPDATE public.tbluserdevicetokens SET isactive = FALSE WHERE userid = v_userid;

        -- Kill any active sessions; refresh tokens are session artifacts, not user data
        DELETE FROM public.tblrefreshtokens WHERE userid = v_userid;

        RETURN json_build_object('status_code',200,'message','User account deleted successfully');

    -- 7 = Self delete, no auth: requires BOTH email and phone to match the same account
    ELSIF p_operation = 7 THEN
        IF v_email IS NULL OR trim(v_email) = '' OR v_phone IS NULL OR trim(v_phone) = '' THEN
            RAISE EXCEPTION 'Both email and phone are required for deletion';
        END IF;

        SELECT id INTO v_userid
        FROM public.tblusermaster
        WHERE lower(trim(email)) = lower(trim(v_email))
          AND lower(trim(phone)) = lower(trim(v_phone))
        LIMIT 1;

        IF v_userid IS NULL THEN
            RETURN json_build_object('status_code',404,'message','No account matches that email and phone combination');
        END IF;

        IF NOT EXISTS (SELECT 1 FROM public.tblusermaster WHERE id = v_userid AND isactive = TRUE) THEN
            RETURN json_build_object('status_code',409,'message','User account is already deleted');
        END IF;

        UPDATE public.tblusermaster
        SET isactive   = FALSE,
            modifiedby = v_userid,
            modifiedon = CURRENT_TIMESTAMP
        WHERE id = v_userid;

        UPDATE public.tbluserdevicetokens SET isactive = FALSE WHERE userid = v_userid;
        DELETE FROM public.tblrefreshtokens WHERE userid = v_userid;

        RETURN json_build_object('status_code',200,'message','User account deleted successfully');

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
END; $function$
