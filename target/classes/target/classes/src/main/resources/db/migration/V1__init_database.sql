-- This script assumes the schema 'ai_study_hub' and the 'citext' extension already exist.
CREATE EXTENSION IF NOT EXISTS citext;
CREATE SCHEMA IF NOT EXISTS ai_study_hub;

-- ==========================================
-- PHẦN 1: QUẢN LÝ NGƯỜI DÙNG & XÁC THỰC
-- ==========================================
-- 2. Bảng Users
CREATE TABLE ai_study_hub.users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(50) NOT NULL UNIQUE,
    email CITEXT NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(255) NOT NULL DEFAULT 'USER', -- Using VARCHAR to match Spring Security Roles
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- THÊM MỚI TỪ BẢNG YÊU CẦU: Bảng Password_Reset_Tokens (Tính năng "Quên mật khẩu")
CREATE TABLE ai_study_hub.password_reset_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES ai_study_hub.users(id) ON DELETE CASCADE,
    token VARCHAR(255) NOT NULL UNIQUE,
    is_used BOOLEAN NOT NULL DEFAULT FALSE,
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_password_tokens_user_id ON ai_study_hub.password_reset_tokens(user_id);
CREATE INDEX idx_password_tokens_token ON ai_study_hub.password_reset_tokens(token);

-- ==========================================
-- PHẦN 2: QUẢN LÝ TÀI LIỆU & LƯU TRỮ
-- ==========================================
-- THÊM MỚI TỪ BẢNG YÊU CẦU: Bảng Subjects (Tính năng "Lọc tài liệu theo môn học")
CREATE TABLE ai_study_hub.subjects (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(50) UNIQUE, -- Mã môn (VD: INT3306)
    name VARCHAR(255) NOT NULL, -- Tên môn (VD: Lập trình Web)
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 3. Bảng Documents
CREATE TABLE ai_study_hub.documents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES ai_study_hub.users(id) ON DELETE CASCADE,
    subject_id UUID REFERENCES ai_study_hub.subjects(id) ON DELETE SET NULL, -- THÊM MỚI: Liên kết với môn học
    title VARCHAR(255) NOT NULL,
    description TEXT,
    file_path VARCHAR(500),
    file_type VARCHAR(20),
    file_size BIGINT,
    visibility VARCHAR(255) NOT NULL DEFAULT 'PUBLIC', -- Matching Enum
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE, -- Giữ Soft Delete
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_documents_user_id ON ai_study_hub.documents(user_id);
CREATE INDEX idx_documents_subject_id ON ai_study_hub.documents(subject_id); -- Index cho bộ lọc môn học

-- 4. Bảng Tags (Hỗ trợ phân loại chi tiết hơn ngoài môn học)
CREATE TABLE ai_study_hub.tags (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL UNIQUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 5. Bảng Trung gian Document_Tags
CREATE TABLE ai_study_hub.document_tags (
    document_id UUID NOT NULL REFERENCES ai_study_hub.documents(id) ON DELETE CASCADE,
    tag_id UUID NOT NULL REFERENCES ai_study_hub.tags(id) ON DELETE CASCADE,
    PRIMARY KEY (document_id, tag_id)
);

CREATE INDEX idx_document_tags_tag_id ON ai_study_hub.document_tags(tag_id);

-- 6. Bảng Cloud_Files (Upload file lên cloud)
CREATE TABLE ai_study_hub.cloud_files (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id UUID NOT NULL UNIQUE REFERENCES ai_study_hub.documents(id) ON DELETE CASCADE,
    provider VARCHAR(20),
    cloud_url VARCHAR(500) NOT NULL,
    cloud_key VARCHAR(300) NOT NULL,
    status VARCHAR(255) DEFAULT 'PENDING', -- Matching Enum
    uploaded_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW() -- Đã bổ sung cập nhật trạng thái
);

-- ==========================================
-- PHẦN 3: AI CHATBOT MODULE
-- ==========================================
-- 7. Bảng Chat_Sessions (Quản lý phiên chat)
CREATE TABLE ai_study_hub.chat_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES ai_study_hub.users(id) ON DELETE CASCADE,
    title VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_chat_sessions_user_id ON ai_study_hub.chat_sessions(user_id);

-- 8. Bảng Chat_Messages (Lưu trữ nội dung hỏi đáp)
CREATE TABLE ai_study_hub.chat_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES ai_study_hub.chat_sessions(id) ON DELETE CASCADE,
    role VARCHAR(255) NOT NULL, -- Matching Enum
    content TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_chat_messages_session_id ON ai_study_hub.chat_messages(session_id);

-- 9. Bảng Chat_Document_Context (Ngữ cảnh RAG: Chatbot lấy thông tin từ doc nào)
CREATE TABLE ai_study_hub.chat_document_context (
    session_id UUID NOT NULL REFERENCES ai_study_hub.chat_sessions(id) ON DELETE CASCADE,
    document_id UUID NOT NULL REFERENCES ai_study_hub.documents(id) ON DELETE CASCADE,
    PRIMARY KEY (session_id, document_id)
);

CREATE INDEX idx_chat_doc_context_doc_id ON ai_study_hub.chat_document_context(document_id);
