-- ============================================================
-- Lược đồ CSDL SQL Server - Hệ thống thuyết minh tự động đa ngôn ngữ Chùa Linh Ứng
-- Sinh tự động từ app/db/tables.py (Code First). Ứng dụng tự tạo các bảng này lúc khởi động;
-- file này để đọc / chạy tay trong SQL Server Management Studio (SSMS).
-- Mọi khóa ngoại: ON DELETE NO ACTION -> không xóa được bản ghi cha khi còn bản ghi con tham chiếu.
-- ============================================================

IF DB_ID(N'audio_guide') IS NULL CREATE DATABASE [audio_guide];
GO
USE [audio_guide];
GO

CREATE TABLE admin_users (
    id VARCHAR(32) NOT NULL,
    username NVARCHAR(100) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    full_name NVARCHAR(255) NOT NULL,
    role VARCHAR(20) NOT NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2(6) NOT NULL,
    last_login_at DATETIME2(6) NULL,
    CONSTRAINT pk_admin_users PRIMARY KEY (id),
    CONSTRAINT uq_admin_users_username UNIQUE (username)
);
GO

CREATE TABLE chat_cache (
    [key] VARCHAR(64) NOT NULL,
    answer NVARCHAR(max) NOT NULL,
    expires_at DATETIME2(6) NOT NULL,
    CONSTRAINT pk_chat_cache PRIMARY KEY ([key])
);
CREATE INDEX ix_chat_cache_expires_at ON chat_cache (expires_at);
GO

CREATE TABLE knowledge_articles (
    id VARCHAR(32) NOT NULL,
    title NVARCHAR(255) NOT NULL,
    content NVARCHAR(max) NOT NULL,
    tags NVARCHAR(max) NOT NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2(6) NOT NULL,
    updated_at DATETIME2(6) NOT NULL,
    CONSTRAINT pk_knowledge_articles PRIMARY KEY (id)
);
GO

CREATE TABLE languages (
    id VARCHAR(32) NOT NULL,
    code NVARCHAR(16) NOT NULL,
    name NVARCHAR(100) NOT NULL,
    native_name NVARCHAR(100) NOT NULL,
    translator_code NVARCHAR(16) NOT NULL,
    tts_voice NVARCHAR(100) NOT NULL,
    is_active BIT NOT NULL,
    sort_order INTEGER NOT NULL,
    CONSTRAINT pk_languages PRIMARY KEY (id),
    CONSTRAINT uq_languages_code UNIQUE (code)
);
GO

CREATE TABLE media_files (
    [key] NVARCHAR(255) NOT NULL,
    data VARBINARY(max) NOT NULL,
    content_type VARCHAR(100) NOT NULL,
    size INTEGER NOT NULL,
    updated_at DATETIME2(6) NOT NULL,
    CONSTRAINT pk_media_files PRIMARY KEY ([key])
);
GO

CREATE TABLE pois (
    id VARCHAR(32) NOT NULL,
    code NVARCHAR(64) NOT NULL,
    name NVARCHAR(255) NOT NULL,
    description NVARCHAR(max) NOT NULL,
    category NVARCHAR(50) NOT NULL,
    latitude FLOAT NOT NULL,
    longitude FLOAT NOT NULL,
    trigger_radius_m INTEGER NOT NULL,
    priority INTEGER NOT NULL,
    thumbnail_url NVARCHAR(1000) NULL,
    image_urls NVARCHAR(max) NOT NULL,
    sort_order INTEGER NOT NULL,
    is_active BIT NOT NULL,
    opening_hours NVARCHAR(max) NULL,
    entry_fee_vnd INTEGER NOT NULL,
    visit_minutes INTEGER NULL,
    amenities NVARCHAR(max) NOT NULL,
    tips NVARCHAR(max) NOT NULL,
    created_at DATETIME2(6) NOT NULL,
    updated_at DATETIME2(6) NOT NULL,
    CONSTRAINT pk_pois PRIMARY KEY (id),
    CONSTRAINT uq_pois_code UNIQUE (code)
);
CREATE INDEX ix_pois_is_active_sort_order ON pois (is_active, sort_order);
GO

CREATE TABLE tours (
    id VARCHAR(32) NOT NULL,
    code NVARCHAR(64) NOT NULL,
    name NVARCHAR(255) NOT NULL,
    description NVARCHAR(max) NULL,
    estimated_minutes INTEGER NOT NULL,
    translation_requested_at NVARCHAR(max) NOT NULL,
    is_active BIT NOT NULL,
    created_at DATETIME2(6) NOT NULL,
    updated_at DATETIME2(6) NOT NULL,
    CONSTRAINT pk_tours PRIMARY KEY (id),
    CONSTRAINT uq_tours_code UNIQUE (code)
);
GO

CREATE TABLE access_sessions (
    id VARCHAR(32) NOT NULL,
    code NVARCHAR(64) NOT NULL,
    method VARCHAR(20) NOT NULL,
    status VARCHAR(20) NOT NULL,
    amount INTEGER NOT NULL,
    currency VARCHAR(8) NOT NULL,
    created_by VARCHAR(32) NULL,
    note NVARCHAR(500) NULL,
    created_at DATETIME2(6) NOT NULL,
    code_expires_at DATETIME2(6) NOT NULL,
    paid_at DATETIME2(6) NULL,
    activated_at DATETIME2(6) NULL,
    access_expires_at DATETIME2(6) NULL,
    CONSTRAINT pk_access_sessions PRIMARY KEY (id),
    CONSTRAINT uq_access_sessions_code UNIQUE (code),
    CONSTRAINT fk_access_sessions_created_by FOREIGN KEY(created_by) REFERENCES admin_users (id)
);
CREATE INDEX ix_access_sessions_created_by ON access_sessions (created_by);
CREATE INDEX ix_access_sessions_status_created_at ON access_sessions (status, created_at);
GO

CREATE TABLE poi_localizations (
    id VARCHAR(32) NOT NULL,
    poi_id VARCHAR(32) NOT NULL,
    lang NVARCHAR(16) NOT NULL,
    name NVARCHAR(255) NULL,
    description NVARCHAR(max) NULL,
    audio_url NVARCHAR(500) NULL,
    status VARCHAR(20) NOT NULL,
    translated_by VARCHAR(20) NOT NULL,
    source_hash VARCHAR(64) NULL,
    tips NVARCHAR(max) NULL,
    tips_hash VARCHAR(64) NULL,
    error NVARCHAR(max) NULL,
    updated_at DATETIME2(6) NOT NULL,
    CONSTRAINT pk_poi_localizations PRIMARY KEY (id),
    CONSTRAINT uq_poi_localizations_poi_id_lang UNIQUE (poi_id, lang),
    CONSTRAINT fk_poi_localizations_poi_id FOREIGN KEY(poi_id) REFERENCES pois (id),
    CONSTRAINT fk_poi_localizations_lang FOREIGN KEY(lang) REFERENCES languages (code)
);
CREATE INDEX ix_poi_localizations_lang ON poi_localizations (lang);
CREATE INDEX ix_poi_localizations_status ON poi_localizations (status);
GO

CREATE TABLE tour_pois (
    tour_id VARCHAR(32) NOT NULL,
    poi_id VARCHAR(32) NOT NULL,
    position INTEGER NOT NULL,
    CONSTRAINT pk_tour_pois PRIMARY KEY (tour_id, position),
    CONSTRAINT fk_tour_pois_tour_id FOREIGN KEY(tour_id) REFERENCES tours (id),
    CONSTRAINT fk_tour_pois_poi_id FOREIGN KEY(poi_id) REFERENCES pois (id)
);
CREATE INDEX ix_tour_pois_poi_id ON tour_pois (poi_id);
GO

CREATE TABLE tour_translations (
    tour_id VARCHAR(32) NOT NULL,
    lang NVARCHAR(16) NOT NULL,
    name NVARCHAR(255) NOT NULL,
    description NVARCHAR(max) NULL,
    CONSTRAINT pk_tour_translations PRIMARY KEY (tour_id, lang),
    CONSTRAINT fk_tour_translations_tour_id FOREIGN KEY(tour_id) REFERENCES tours (id),
    CONSTRAINT fk_tour_translations_lang FOREIGN KEY(lang) REFERENCES languages (code)
);
CREATE INDEX ix_tour_translations_lang ON tour_translations (lang);
GO

CREATE TABLE ui_translations (
    lang NVARCHAR(16) NOT NULL,
    strings NVARCHAR(max) NOT NULL,
    source_hash VARCHAR(64) NOT NULL,
    updated_at DATETIME2(6) NOT NULL,
    CONSTRAINT pk_ui_translations PRIMARY KEY (lang),
    CONSTRAINT fk_ui_translations_lang FOREIGN KEY(lang) REFERENCES languages (code)
);
GO

CREATE TABLE chat_logs (
    id VARCHAR(32) NOT NULL,
    session_id VARCHAR(32) NULL,
    lang NVARCHAR(16) NOT NULL,
    question NVARCHAR(max) NOT NULL,
    answer NVARCHAR(max) NOT NULL,
    source_ids NVARCHAR(max) NOT NULL,
    used_llm BIT NOT NULL,
    created_at DATETIME2(6) NOT NULL,
    CONSTRAINT pk_chat_logs PRIMARY KEY (id),
    CONSTRAINT fk_chat_logs_session_id FOREIGN KEY(session_id) REFERENCES access_sessions (id),
    CONSTRAINT fk_chat_logs_lang FOREIGN KEY(lang) REFERENCES languages (code)
);
CREATE INDEX ix_chat_logs_lang ON chat_logs (lang);
CREATE INDEX ix_chat_logs_session_id_created_at ON chat_logs (session_id, created_at);
GO

CREATE TABLE payments (
    id VARCHAR(32) NOT NULL,
    session_id VARCHAR(32) NOT NULL,
    provider NVARCHAR(50) NOT NULL,
    amount INTEGER NOT NULL,
    currency VARCHAR(8) NOT NULL,
    status VARCHAR(20) NOT NULL,
    provider_ref NVARCHAR(255) NULL,
    created_at DATETIME2(6) NOT NULL,
    completed_at DATETIME2(6) NULL,
    CONSTRAINT pk_payments PRIMARY KEY (id),
    CONSTRAINT fk_payments_session_id FOREIGN KEY(session_id) REFERENCES access_sessions (id)
);
CREATE INDEX ix_payments_session_id ON payments (session_id);
GO

CREATE TABLE visit_events (
    id VARCHAR(32) NOT NULL,
    session_id VARCHAR(32) NULL,
    poi_id VARCHAR(32) NOT NULL,
    lang NVARCHAR(16) NOT NULL,
    type VARCHAR(20) NOT NULL,
    created_at DATETIME2(6) NOT NULL,
    CONSTRAINT pk_visit_events PRIMARY KEY (id),
    CONSTRAINT fk_visit_events_session_id FOREIGN KEY(session_id) REFERENCES access_sessions (id),
    CONSTRAINT fk_visit_events_poi_id FOREIGN KEY(poi_id) REFERENCES pois (id)
);
CREATE INDEX ix_visit_events_poi_id ON visit_events (poi_id);
CREATE INDEX ix_visit_events_poi_id_created_at ON visit_events (poi_id, created_at);
CREATE INDEX ix_visit_events_session_id ON visit_events (session_id);
GO

