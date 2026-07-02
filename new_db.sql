-- ==============================================================================
-- AI STUDY HUB - DATABASE SCHEMA (BẢN CHUẨN HÓA)
-- Yêu cầu: PostgreSQL 13+
-- Cách dùng: Chạy toàn bộ file này trong psql hoặc pgAdmin.
--            File tự tạo schema, enums, bảng, trigger, seed data
--            và ghi vào __EFMigrationsHistory để EF Core không chạy migration lại.
-- ==============================================================================
DROP SCHEMA IF EXISTS ai_study_hub CASCADE;
CREATE SCHEMA IF NOT EXISTS ai_study_hub;
SET search_path TO ai_study_hub, public;

-- ==========================================
-- PHẦN 1: ĐỊNH NGHĨA ENUM
-- ==========================================
CREATE TYPE ai_study_hub.doc_visibility   AS ENUM ('public', 'private');
CREATE TYPE ai_study_hub.cloud_status     AS ENUM ('pending', 'uploaded', 'failed');
CREATE TYPE ai_study_hub.chat_role        AS ENUM ('user', 'assistant');
CREATE TYPE ai_study_hub.payment_status   AS ENUM ('pending', 'completed', 'failed', 'refunded');
CREATE TYPE ai_study_hub.payment_method   AS ENUM ('vnpay', 'momo', 'stripe', 'bank_transfer');
CREATE TYPE ai_study_hub.purchase_type    AS ENUM ('storage_package', 'subscription_package');
CREATE TYPE ai_study_hub.user_role        AS ENUM ('user', 'admin');

-- ==========================================
-- PHẦN 2: TẠO BẢNG DỮ LIỆU
-- ==========================================

-- 1. roles
CREATE TABLE ai_study_hub.roles (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    name        VARCHAR(50)  NOT NULL UNIQUE,
    description TEXT,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

INSERT INTO ai_study_hub.roles (name, description) VALUES
    ('user',  'Người dùng thông thường, có quyền upload tài liệu và chat với AI'),
    ('admin', 'Quản trị viên toàn quyền hệ thống')
ON CONFLICT (name) DO NOTHING;

-- 2. users
CREATE TABLE ai_study_hub.users (
    id                   UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    username             VARCHAR(50)  NOT NULL UNIQUE,
    email                VARCHAR(255) NOT NULL,
    password_hash        VARCHAR(255) NOT NULL,
    role_id              UUID         REFERENCES ai_study_hub.roles(id) ON DELETE SET NULL,
    refresh_token        VARCHAR(255),
    refresh_token_expiry TIMESTAMPTZ,
    created_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
CREATE UNIQUE INDEX idx_users_email_lower ON ai_study_hub.users (LOWER(email));

-- 3. password_reset_tokens
CREATE TABLE ai_study_hub.password_reset_tokens (
    id         UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id    UUID         NOT NULL REFERENCES ai_study_hub.users(id) ON DELETE CASCADE,
    token      VARCHAR(255) NOT NULL UNIQUE,
    is_used    BOOLEAN      NOT NULL DEFAULT FALSE,
    expires_at TIMESTAMPTZ  NOT NULL,
    created_at TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_password_tokens_user_id ON ai_study_hub.password_reset_tokens(user_id);

-- 4. subjects
CREATE TABLE ai_study_hub.subjects (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    code        VARCHAR(50)  UNIQUE,
    name        VARCHAR(255) NOT NULL,
    description TEXT,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

INSERT INTO ai_study_hub.subjects (code, name) VALUES
    ('MATH101', 'Toán Cao Cấp'),
    ('PHYS101', 'Vật Lý Đại Cương'),
    ('CHEM101', 'Hóa Học Đại Cương'),
    ('CS101',   'Nhập Môn Lập Trình'),
    ('ENG101',  'Tiếng Anh Tổng Quát'),
    ('ECON101', 'Kinh Tế Vi Mô'),
    ('BIO101',  'Sinh Học Đại Cương'),
    ('HIST101', 'Lịch Sử Việt Nam'),
    ('LIT101',  'Văn Học Việt Nam'),
    ('PHI101',  'Triết Học Mác-Lênin')
ON CONFLICT (code) DO NOTHING;

-- 5. documents
CREATE TABLE ai_study_hub.documents (
    id          UUID                        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID                        NOT NULL REFERENCES ai_study_hub.users(id) ON DELETE CASCADE,
    subject_id  UUID                        REFERENCES ai_study_hub.subjects(id) ON DELETE SET NULL,
    title       VARCHAR(255)                NOT NULL,
    description TEXT,
    file_path   VARCHAR(500),
    file_type   VARCHAR(20),
    file_size   BIGINT,
    visibility  ai_study_hub.doc_visibility NOT NULL DEFAULT 'public',
    is_deleted  BOOLEAN                     NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMPTZ                 NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ                 NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_documents_user_id    ON ai_study_hub.documents(user_id);
CREATE INDEX idx_documents_subject_id ON ai_study_hub.documents(subject_id);

-- 6. favorites
CREATE TABLE ai_study_hub.favorites (
    user_id     UUID NOT NULL REFERENCES ai_study_hub.users(id)     ON DELETE CASCADE,
    document_id UUID NOT NULL REFERENCES ai_study_hub.documents(id) ON DELETE CASCADE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (user_id, document_id)
);

-- 7. tags
CREATE TABLE ai_study_hub.tags (
    id         UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    name       VARCHAR(100) NOT NULL UNIQUE,
    created_at TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- 8. document_tags
CREATE TABLE ai_study_hub.document_tags (
    document_id UUID NOT NULL REFERENCES ai_study_hub.documents(id) ON DELETE CASCADE,
    tag_id      UUID NOT NULL REFERENCES ai_study_hub.tags(id)      ON DELETE CASCADE,
    PRIMARY KEY (document_id, tag_id)
);

-- 9. cloud_files
CREATE TABLE ai_study_hub.cloud_files (
    id          UUID                      PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id UUID                      NOT NULL UNIQUE REFERENCES ai_study_hub.documents(id) ON DELETE CASCADE,
    provider    VARCHAR(20),
    cloud_url   VARCHAR(500)              NOT NULL,
    cloud_key   VARCHAR(300)              NOT NULL,
    status      ai_study_hub.cloud_status NOT NULL DEFAULT 'pending',
    uploaded_at TIMESTAMPTZ               NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ               NOT NULL DEFAULT NOW()
);

-- 10. user_storage
CREATE TABLE ai_study_hub.user_storage (
    user_id              UUID        PRIMARY KEY REFERENCES ai_study_hub.users(id) ON DELETE CASCADE,
    total_capacity_bytes BIGINT      NOT NULL DEFAULT 10485760,  -- 10 MB
    used_bytes           BIGINT      NOT NULL DEFAULT 0,
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 11. storage_packages
CREATE TABLE ai_study_hub.storage_packages (
    id             UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    name           VARCHAR(100)  NOT NULL,
    capacity_bytes BIGINT        NOT NULL,
    price          DECIMAL(12,2) NOT NULL,
    is_active      BOOLEAN       DEFAULT TRUE,
    created_at     TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

INSERT INTO ai_study_hub.storage_packages (name, capacity_bytes, price) VALUES
    ('Gói 10 GB',   10737418240,   49000),
    ('Gói 50 GB',   53687091200,   99000),
    ('Gói 100 GB', 107374182400,  149000)
ON CONFLICT DO NOTHING;

-- 12. subscription_packages
CREATE TABLE ai_study_hub.subscription_packages (
    id                 UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    name               VARCHAR(100)  NOT NULL,
    description        TEXT,
    price              DECIMAL(12,2) NOT NULL,
    duration_days      INT           NOT NULL,
    ai_chat_limit      INT           NOT NULL DEFAULT 100,
    base_storage_bytes BIGINT        NOT NULL DEFAULT 536870912,
    is_active          BOOLEAN       DEFAULT TRUE,
    created_at         TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at         TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

INSERT INTO ai_study_hub.subscription_packages (name, description, price, duration_days, ai_chat_limit, base_storage_bytes) VALUES
    ('Sinh Viên', 'Dành cho sinh viên: 5 GB lưu trữ, 200 tin nhắn AI/tháng',  29000,  30, 200,  5368709120),
    ('Pro',       'Dành cho học viên chuyên sâu: 20 GB, không giới hạn AI',   99000,  30, 9999, 21474836480),
    ('Pro Năm',   'Gói Pro tiết kiệm 12 tháng: 20 GB, không giới hạn AI',    899000, 365, 9999, 21474836480)
ON CONFLICT DO NOTHING;

-- 13. user_subscriptions
CREATE TABLE ai_study_hub.user_subscriptions (
    id         UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id    UUID        NOT NULL REFERENCES ai_study_hub.users(id)                 ON DELETE CASCADE,
    package_id UUID        NOT NULL REFERENCES ai_study_hub.subscription_packages(id) ON DELETE RESTRICT,
    start_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    end_date   TIMESTAMPTZ NOT NULL,
    status     VARCHAR(20) NOT NULL DEFAULT 'active',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_user_subscriptions_user ON ai_study_hub.user_subscriptions(user_id);

-- 14. transactions
CREATE TABLE ai_study_hub.transactions (
    id                      UUID                        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                 UUID                        NOT NULL REFERENCES ai_study_hub.users(id)                 ON DELETE CASCADE,
    purchase_kind           ai_study_hub.purchase_type  NOT NULL DEFAULT 'storage_package',
    package_id              UUID                        REFERENCES ai_study_hub.storage_packages(id)               ON DELETE SET NULL,
    subscription_package_id UUID                        REFERENCES ai_study_hub.subscription_packages(id)          ON DELETE SET NULL,
    amount                  DECIMAL(12,2)               NOT NULL,
    storage_added_bytes     BIGINT                      NOT NULL DEFAULT 0,
    status                  ai_study_hub.payment_status NOT NULL DEFAULT 'pending',
    method                  ai_study_hub.payment_method NOT NULL,
    transaction_ref         VARCHAR(255),
    created_at              TIMESTAMPTZ                 NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ                 NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_transactions_user_id ON ai_study_hub.transactions(user_id);

-- 15. chat_sessions
CREATE TABLE ai_study_hub.chat_sessions (
    id         UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id    UUID         NOT NULL REFERENCES ai_study_hub.users(id) ON DELETE CASCADE,
    title      VARCHAR(255),
    created_at TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- 16. chat_messages
CREATE TABLE ai_study_hub.chat_messages (
    id         UUID                   PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID                   NOT NULL REFERENCES ai_study_hub.chat_sessions(id) ON DELETE CASCADE,
    role       ai_study_hub.chat_role NOT NULL,
    content    TEXT                   NOT NULL,
    created_at TIMESTAMPTZ            NOT NULL DEFAULT NOW()
);

-- 17. chat_document_context
CREATE TABLE ai_study_hub.chat_document_context (
    session_id  UUID NOT NULL REFERENCES ai_study_hub.chat_sessions(id) ON DELETE CASCADE,
    document_id UUID NOT NULL REFERENCES ai_study_hub.documents(id)     ON DELETE CASCADE,
    PRIMARY KEY (session_id, document_id)
);

-- ==========================================
-- PHẦN 3: HÀM VÀ TRIGGER
-- ==========================================

-- Hàm 1: Tự động cập nhật cột updated_at
CREATE OR REPLACE FUNCTION ai_study_hub.update_modified_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Hàm 2: Tự tạo ví lưu trữ khi user đăng ký
CREATE OR REPLACE FUNCTION ai_study_hub.create_user_storage_on_signup()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO ai_study_hub.user_storage (user_id) VALUES (NEW.id);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Hàm 3: Cộng/trừ dung lượng khi thanh toán thành công / hoàn tiền
CREATE OR REPLACE FUNCTION ai_study_hub.update_storage_and_sub_on_payment_success()
RETURNS TRIGGER AS $$
DECLARE
    v_duration_days INT;
    v_base_storage  BIGINT;
BEGIN
    IF NEW.status = 'completed' AND OLD.status <> 'completed' THEN
        IF NEW.purchase_kind = 'storage_package' THEN
            UPDATE ai_study_hub.user_storage
            SET total_capacity_bytes = total_capacity_bytes + NEW.storage_added_bytes
            WHERE user_id = NEW.user_id;

        ELSIF NEW.purchase_kind = 'subscription_package' THEN
            SELECT duration_days, base_storage_bytes
            INTO   v_duration_days, v_base_storage
            FROM   ai_study_hub.subscription_packages
            WHERE  id = NEW.subscription_package_id;

            UPDATE ai_study_hub.user_subscriptions
            SET status = 'expired'
            WHERE user_id = NEW.user_id AND status = 'active';

            INSERT INTO ai_study_hub.user_subscriptions
                (user_id, package_id, start_date, end_date, status)
            VALUES
                (NEW.user_id, NEW.subscription_package_id,
                 NOW(), NOW() + (v_duration_days || ' days')::INTERVAL, 'active');

            UPDATE ai_study_hub.user_storage
            SET total_capacity_bytes = total_capacity_bytes + v_base_storage
            WHERE user_id = NEW.user_id;
        END IF;

    ELSIF NEW.status = 'refunded' AND OLD.status = 'completed' THEN
        IF NEW.purchase_kind = 'storage_package' THEN
            UPDATE ai_study_hub.user_storage
            SET total_capacity_bytes = total_capacity_bytes - NEW.storage_added_bytes
            WHERE user_id = NEW.user_id;

        ELSIF NEW.purchase_kind = 'subscription_package' THEN
            SELECT base_storage_bytes INTO v_base_storage
            FROM   ai_study_hub.subscription_packages
            WHERE  id = NEW.subscription_package_id;

            UPDATE ai_study_hub.user_subscriptions
            SET status = 'cancelled'
            WHERE user_id = NEW.user_id
              AND package_id = NEW.subscription_package_id
              AND status = 'active';

            UPDATE ai_study_hub.user_storage
            SET total_capacity_bytes = total_capacity_bytes - v_base_storage
            WHERE user_id = NEW.user_id;
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Hàm 4: Tự động cập nhật used_bytes khi thêm/sửa/xóa document
CREATE OR REPLACE FUNCTION ai_study_hub.update_used_storage_on_doc_change()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        IF COALESCE(NEW.is_deleted, FALSE) = FALSE THEN
            UPDATE ai_study_hub.user_storage
            SET used_bytes = used_bytes + COALESCE(NEW.file_size, 0)
            WHERE user_id = NEW.user_id;
        END IF;
        RETURN NEW;

    ELSIF TG_OP = 'DELETE' THEN
        IF COALESCE(OLD.is_deleted, FALSE) = FALSE THEN
            UPDATE ai_study_hub.user_storage
            SET used_bytes = used_bytes - COALESCE(OLD.file_size, 0)
            WHERE user_id = OLD.user_id;
        END IF;
        RETURN OLD;

    ELSIF TG_OP = 'UPDATE' THEN
        IF NEW.is_deleted = TRUE AND OLD.is_deleted = FALSE THEN
            UPDATE ai_study_hub.user_storage
            SET used_bytes = used_bytes - COALESCE(OLD.file_size, 0)
            WHERE user_id = NEW.user_id;
        ELSIF NEW.is_deleted = FALSE AND OLD.is_deleted = TRUE THEN
            UPDATE ai_study_hub.user_storage
            SET used_bytes = used_bytes + COALESCE(NEW.file_size, 0)
            WHERE user_id = NEW.user_id;
        ELSIF NEW.is_deleted = FALSE AND OLD.is_deleted = FALSE THEN
            UPDATE ai_study_hub.user_storage
            SET used_bytes = used_bytes + (COALESCE(NEW.file_size, 0) - COALESCE(OLD.file_size, 0))
            WHERE user_id = NEW.user_id;
        END IF;
        RETURN NEW;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- ==========================================
-- PHẦN 4: GẮN TRIGGER VÀO BẢNG
-- ==========================================

CREATE TRIGGER update_user_modtime
    BEFORE UPDATE ON ai_study_hub.users
    FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();

CREATE TRIGGER update_subject_modtime
    BEFORE UPDATE ON ai_study_hub.subjects
    FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();

CREATE TRIGGER update_doc_modtime
    BEFORE UPDATE ON ai_study_hub.documents
    FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();

CREATE TRIGGER update_cloud_files_modtime
    BEFORE UPDATE ON ai_study_hub.cloud_files
    FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();

CREATE TRIGGER update_chat_modtime
    BEFORE UPDATE ON ai_study_hub.chat_sessions
    FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();

CREATE TRIGGER update_user_storage_modtime
    BEFORE UPDATE ON ai_study_hub.user_storage
    FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();

CREATE TRIGGER update_transactions_modtime
    BEFORE UPDATE ON ai_study_hub.transactions
    FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();

CREATE TRIGGER update_sub_packages_modtime
    BEFORE UPDATE ON ai_study_hub.subscription_packages
    FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();

CREATE TRIGGER trigger_create_user_storage
    AFTER INSERT ON ai_study_hub.users
    FOR EACH ROW EXECUTE FUNCTION ai_study_hub.create_user_storage_on_signup();

CREATE TRIGGER trigger_update_storage_on_payment
    AFTER UPDATE ON ai_study_hub.transactions
    FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_storage_and_sub_on_payment_success();

CREATE TRIGGER trigger_update_used_storage
    AFTER INSERT OR UPDATE OR DELETE ON ai_study_hub.documents
    FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_used_storage_on_doc_change();

-- ==========================================
-- PHẦN 5: EF CORE MIGRATION HISTORY
-- (Ngăn EF Core chạy lại migration đã áp dụng)
-- ==========================================
CREATE TABLE IF NOT EXISTS ai_study_hub."__EFMigrationsHistory" (
    "MigrationId"    CHARACTER VARYING(150) NOT NULL,
    "ProductVersion" CHARACTER VARYING(32)  NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

INSERT INTO ai_study_hub."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260616064723_InitialCreate', '10.0.8')
ON CONFLICT ("MigrationId") DO NOTHING;
