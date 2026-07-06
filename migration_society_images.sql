-- ============================================================
-- Migration: Society Images — upload & get per property
-- Run in pgAdmin
-- ============================================================

-- Step 1: Create table
CREATE TABLE IF NOT EXISTS public.tblsocietyimages (
    id          serial PRIMARY KEY,
    propertyid  integer NOT NULL,
    imageurl    text    NOT NULL,
    imagetitle  text,
    uploadedby  integer NOT NULL,
    createdon   timestamp with time zone DEFAULT now()
);

ALTER TABLE public.tblsocietyimages OWNER TO nandi_316q_user;


-- Step 2: Create SP
CREATE OR REPLACE FUNCTION public.sp_api_societyimages(p_operation integer, p_json text DEFAULT NULL::text)
RETURNS json LANGUAGE plpgsql AS $$
DECLARE
    v_result        json;
    v_id            int;
    v_propertyid    int;
    v_imageurl      text;
    v_imagetitle    text;
    v_uploadedby    int;
    v_error_message text;
    v_error_code    text;
    v_json          json;
BEGIN
    IF p_json IS NOT NULL THEN
        v_propertyid := NULLIF(p_json::json->>'propertyid', '')::int;
        v_imageurl   := NULLIF(trim(p_json::json->>'imageurl'), '');
        v_imagetitle := NULLIF(trim(p_json::json->>'imagetitle'), '');
        v_uploadedby := NULLIF(p_json::json->>'uploadedby', '')::int;
    END IF;

    -- Op 1: Get all images for a property
    IF p_operation = 1 THEN
        IF v_propertyid IS NULL THEN
            RETURN json_build_object('status_code', 400, 'message', 'propertyid is required');
        END IF;

        SELECT COALESCE(json_agg(t ORDER BY createdon DESC), '[]'::json) INTO v_result
        FROM (
            SELECT
                si.id,
                si.propertyid,
                si.imageurl,
                si.imagetitle,
                si.uploadedby,
                CONCAT(u.firstname, ' ', u.lastname) AS uploadedbyname,
                si.createdon
            FROM public.tblsocietyimages si
            LEFT JOIN public.tblusermaster u ON si.uploadedby = u.id
            WHERE si.propertyid = v_propertyid
        ) t;

        RETURN json_build_object('status_code', 200, 'message', 'Society images fetched successfully', 'data', v_result);

    -- Op 2: Insert a single image
    ELSIF p_operation = 2 THEN
        IF v_propertyid IS NULL THEN RETURN json_build_object('status_code', 400, 'message', 'propertyid is required'); END IF;
        IF v_imageurl   IS NULL THEN RETURN json_build_object('status_code', 400, 'message', 'imageurl is required');   END IF;
        IF v_uploadedby IS NULL THEN RETURN json_build_object('status_code', 400, 'message', 'uploadedby is required'); END IF;

        INSERT INTO public.tblsocietyimages (propertyid, imageurl, imagetitle, uploadedby, createdon)
        VALUES (v_propertyid, v_imageurl, v_imagetitle, v_uploadedby, NOW())
        RETURNING id INTO v_id;

        RETURN json_build_object('status_code', 201, 'message', 'Image uploaded successfully', 'data', json_build_object('id', v_id));

    ELSE
        RETURN json_build_object('status_code', 400, 'message', 'Invalid operation');
    END IF;

EXCEPTION WHEN OTHERS THEN
    v_error_message := SQLERRM;
    v_error_code    := SQLSTATE;
    v_json := json_build_object(
        'error_message', v_error_message, 'error_code', v_error_code,
        'error_source',  'sp_api_societyimages',
        'metadata',      'Operation: '||p_operation||', Data: '||COALESCE(p_json,'')::text,
        'created_on',    TO_CHAR(CURRENT_TIMESTAMP, 'YYYY-MM-DD HH24:MI:SS')
    );
    PERFORM sp_error_logs(2, v_json::text);
    RETURN json_build_object('status_code', 500, 'message', 'An error occurred', 'error', v_error_message);
END; $$;
