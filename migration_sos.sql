-- ============================================================
-- Migration: SOS Contacts feature
-- Run in pgAdmin
-- ============================================================

-- Step 1: Create table
CREATE TABLE IF NOT EXISTS public.tblsoscontacts (
    id            serial PRIMARY KEY,
    userid        integer NOT NULL,
    contactuserid integer NOT NULL,
    relation      text,
    createdon     timestamp with time zone DEFAULT now(),
    CONSTRAINT uq_sos_user_contact UNIQUE (userid, contactuserid)
);

ALTER TABLE public.tblsoscontacts OWNER TO nandi_316q_user;


-- Step 2: SP — sp_api_soscontacts
CREATE OR REPLACE FUNCTION public.sp_api_soscontacts(p_operation integer, p_json text DEFAULT NULL::text)
RETURNS json LANGUAGE plpgsql AS $$
DECLARE
    v_result        json;
    v_id            int;
    v_userid        int;
    v_contactuserid int;
    v_relation      text;
    v_error_message text;
    v_error_code    text;
    v_json          json;
BEGIN
    IF p_json IS NOT NULL THEN
        v_id            := NULLIF(p_json::json->>'id', '')::int;
        v_userid        := NULLIF(p_json::json->>'userid', '')::int;
        v_contactuserid := NULLIF(p_json::json->>'contactuserid', '')::int;
        v_relation      := NULLIF(trim(p_json::json->>'relation'), '');
    END IF;

    -- Op 1: Get SOS contacts for a user (with contact user details)
    IF p_operation = 1 THEN
        IF v_userid IS NULL THEN
            RETURN json_build_object('status_code', 400, 'message', 'userid is required');
        END IF;

        SELECT COALESCE(json_agg(t ORDER BY t.createdon ASC), '[]'::json) INTO v_result
        FROM (
            SELECT
                s.id,
                s.userid,
                s.contactuserid,
                CONCAT(u.firstname, ' ', u.lastname)  AS contactfullname,
                u.email                                AS contactemail,
                u.phone                                AS contactphone,
                u.profileimage                         AS contactprofileimage,
                s.relation,
                s.createdon
            FROM public.tblsoscontacts s
            INNER JOIN public.tblusermaster u ON s.contactuserid = u.id
            WHERE s.userid = v_userid
        ) t;

        RETURN json_build_object('status_code', 200, 'message', 'SOS contacts fetched successfully', 'data', v_result);

    -- Op 2: Add SOS contact
    ELSIF p_operation = 2 THEN
        IF v_userid IS NULL        THEN RETURN json_build_object('status_code', 400, 'message', 'userid is required');        END IF;
        IF v_contactuserid IS NULL THEN RETURN json_build_object('status_code', 400, 'message', 'contactuserid is required'); END IF;
        IF v_userid = v_contactuserid THEN RETURN json_build_object('status_code', 400, 'message', 'Cannot add yourself as SOS contact'); END IF;

        -- Prevent duplicate
        IF EXISTS (SELECT 1 FROM public.tblsoscontacts WHERE userid = v_userid AND contactuserid = v_contactuserid) THEN
            RETURN json_build_object('status_code', 409, 'message', 'This user is already in your SOS contacts');
        END IF;

        INSERT INTO public.tblsoscontacts (userid, contactuserid, relation, createdon)
        VALUES (v_userid, v_contactuserid, v_relation, NOW())
        RETURNING id INTO v_id;

        RETURN json_build_object('status_code', 201, 'message', 'SOS contact added successfully', 'data', json_build_object('id', v_id));

    -- Op 3: Remove SOS contact by id
    ELSIF p_operation = 3 THEN
        IF v_id IS NULL THEN RETURN json_build_object('status_code', 400, 'message', 'id is required'); END IF;

        DELETE FROM public.tblsoscontacts WHERE id = v_id AND userid = v_userid;

        IF NOT FOUND THEN
            RETURN json_build_object('status_code', 404, 'message', 'SOS contact not found');
        END IF;

        RETURN json_build_object('status_code', 200, 'message', 'SOS contact removed successfully');

    -- Op 4: Update relation
    ELSIF p_operation = 4 THEN
        IF v_id IS NULL THEN RETURN json_build_object('status_code', 400, 'message', 'id is required'); END IF;

        UPDATE public.tblsoscontacts
        SET relation = v_relation
        WHERE id = v_id AND userid = v_userid;

        IF NOT FOUND THEN
            RETURN json_build_object('status_code', 404, 'message', 'SOS contact not found');
        END IF;

        RETURN json_build_object('status_code', 200, 'message', 'SOS contact updated successfully');

    -- Op 5: Get device tokens of all SOS contacts (used for push notification)
    ELSIF p_operation = 5 THEN
        IF v_userid IS NULL THEN RETURN json_build_object('status_code', 400, 'message', 'userid is required'); END IF;

        SELECT COALESCE(json_agg(t), '[]'::json) INTO v_result
        FROM (
            SELECT DISTINCT dt.devicetoken
            FROM public.tblsoscontacts s
            INNER JOIN public.tbluserdevicetokens dt ON s.contactuserid = dt.userid
            WHERE s.userid = v_userid
              AND dt.devicetoken IS NOT NULL
              AND dt.devicetoken <> ''
        ) t;

        RETURN json_build_object('status_code', 200, 'message', 'Device tokens fetched', 'data', v_result);

    ELSE
        RETURN json_build_object('status_code', 400, 'message', 'Invalid operation');
    END IF;

EXCEPTION WHEN OTHERS THEN
    v_error_message := SQLERRM;
    v_error_code    := SQLSTATE;
    v_json := json_build_object(
        'error_message', v_error_message, 'error_code', v_error_code,
        'error_source',  'sp_api_soscontacts',
        'metadata',      'Operation: '||p_operation||', Data: '||COALESCE(p_json,'')::text,
        'created_on',    TO_CHAR(CURRENT_TIMESTAMP, 'YYYY-MM-DD HH24:MI:SS')
    );
    PERFORM sp_error_logs(2, v_json::text);
    RETURN json_build_object('status_code', 500, 'message', 'An error occurred', 'error', v_error_message);
END; $$;
