-- =========================
-- 0) EXTENSIONES
-- =========================
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- =========================
-- 1) SEGURIDAD / RBAC
-- =========================
CREATE TABLE users (
  user_id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  username           TEXT UNIQUE NOT NULL,
  full_name          TEXT NOT NULL,
  email              TEXT,
  password_hash      TEXT NOT NULL,
  is_active          BOOLEAN NOT NULL DEFAULT TRUE,
  must_change_pwd    BOOLEAN NOT NULL DEFAULT FALSE,
  last_login_at      TIMESTAMPTZ,
  created_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE roles (
  role_id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  role_name          TEXT UNIQUE NOT NULL,
  description        TEXT,
  created_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE permissions (
  permission_id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  permission_key     TEXT UNIQUE NOT NULL,
  description        TEXT
);

CREATE TABLE user_roles (
  user_id            UUID NOT NULL REFERENCES users(user_id),
  role_id            UUID NOT NULL REFERENCES roles(role_id),
  PRIMARY KEY (user_id, role_id)
);

CREATE TABLE role_permissions (
  role_id            UUID NOT NULL REFERENCES roles(role_id),
  permission_id      UUID NOT NULL REFERENCES permissions(permission_id),
  PRIMARY KEY (role_id, permission_id)
);

-- =========================
-- 2) AUDIT TRAIL
-- =========================
CREATE TABLE audit_log (
  audit_id           BIGSERIAL PRIMARY KEY,
  occurred_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
  user_id            UUID REFERENCES users(user_id),
  actor_username     TEXT,
  action             TEXT NOT NULL,
  entity_type        TEXT NOT NULL,
  entity_id          UUID,
  details_json       JSONB NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX idx_audit_entity ON audit_log(entity_type, entity_id);
CREATE INDEX idx_audit_time   ON audit_log(occurred_at);

-- =========================
-- 3) DOCUMENTOS
-- =========================
CREATE TABLE document_types (
  document_type_id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  type_code          TEXT UNIQUE NOT NULL,
  name               TEXT NOT NULL
);

CREATE TABLE documents (
  document_id        UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  doc_code           TEXT UNIQUE NOT NULL,
  title              TEXT NOT NULL,
  document_type_id   UUID NOT NULL REFERENCES document_types(document_type_id),
  area               TEXT,
  process            TEXT,
  owner_user_id      UUID REFERENCES users(user_id),
  status             TEXT NOT NULL CHECK (status IN ('DRAFT','IN_REVIEW','APPROVED','OBSOLETE','RETIRED')),
  review_interval_months INT,
  next_review_due    DATE,
  created_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_documents_status ON documents(status);
CREATE INDEX idx_documents_type   ON documents(document_type_id);

CREATE TABLE document_versions (
  document_version_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  document_id         UUID NOT NULL REFERENCES documents(document_id) ON DELETE CASCADE,
  version_major       INT NOT NULL,
  version_minor       INT NOT NULL DEFAULT 0,
  version_label       TEXT NOT NULL,
  change_summary      TEXT NOT NULL,
  created_by_user_id  UUID REFERENCES users(user_id),
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
  effective_from      TIMESTAMPTZ,
  cloud_file_id       TEXT,
  cloud_etag          TEXT,
  sha256              TEXT,
  mime_type           TEXT,
  file_name           TEXT,
  is_current          BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE UNIQUE INDEX uq_doc_current ON document_versions(document_id) WHERE is_current = TRUE;
CREATE INDEX idx_doc_versions_doc  ON document_versions(document_id);

CREATE TABLE document_approvals (
  approval_id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  document_version_id UUID NOT NULL REFERENCES document_versions(document_version_id) ON DELETE CASCADE,
  step               TEXT NOT NULL CHECK (step IN ('AUTHOR','REVIEW','APPROVAL')),
  decision           TEXT CHECK (decision IN ('PENDING','APPROVED','REJECTED')) DEFAULT 'PENDING',
  decided_by_user_id UUID REFERENCES users(user_id),
  decided_at         TIMESTAMPTZ,
  comments           TEXT
);

CREATE TABLE document_acknowledgements (
  document_version_id UUID NOT NULL REFERENCES document_versions(document_version_id) ON DELETE CASCADE,
  user_id             UUID NOT NULL REFERENCES users(user_id),
  acknowledged_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (document_version_id, user_id)
);

CREATE TABLE document_print_events (
  print_event_id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  document_version_id UUID NOT NULL REFERENCES document_versions(document_version_id),
  user_id             UUID REFERENCES users(user_id),
  printed_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
  print_type          TEXT NOT NULL CHECK (print_type IN ('CONTROLLED','UNCONTROLLED')),
  copy_identifier     TEXT NOT NULL,
  watermark_text      TEXT
);

-- =========================
-- 4) STORAGE / SYNC
-- =========================
CREATE TABLE storage_providers (
  provider_id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  provider_type       TEXT NOT NULL CHECK (provider_type IN ('GOOGLE_DRIVE','ONEDRIVE','SHAREPOINT','S3','SMB','OTHER')),
  name                TEXT NOT NULL,
  is_primary          BOOLEAN NOT NULL DEFAULT FALSE,
  config_json         JSONB NOT NULL DEFAULT '{}'::jsonb
);

CREATE UNIQUE INDEX uq_primary_provider ON storage_providers(is_primary) WHERE is_primary = TRUE;

CREATE TABLE devices (
  device_id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  device_name         TEXT NOT NULL,
  os_version          TEXT,
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE sync_events (
  sync_event_id       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  occurred_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
  device_id            UUID REFERENCES devices(device_id),
  user_id              UUID REFERENCES users(user_id),
  event_type           TEXT NOT NULL CHECK (event_type IN ('DOWNLOAD','UPLOAD','CONFLICT','CHECKOUT','CHECKIN','ERROR')),
  entity_type          TEXT NOT NULL,
  entity_id            UUID,
  details_json         JSONB NOT NULL DEFAULT '{}'::jsonb
);

-- =========================
-- 5) PERSONAL Y FORMACIÓN
-- =========================
CREATE TABLE staff_profiles (
  staff_id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id             UUID UNIQUE REFERENCES users(user_id),
  position_title      TEXT,
  department          TEXT,
  hired_at            DATE,
  is_active           BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE training_events (
  training_id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  title               TEXT NOT NULL,
  provider            TEXT,
  training_type       TEXT,
  started_at          DATE,
  ended_at            DATE,
  evidence_doc_id     UUID REFERENCES documents(document_id),
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE staff_training (
  staff_id            UUID NOT NULL REFERENCES staff_profiles(staff_id) ON DELETE CASCADE,
  training_id         UUID NOT NULL REFERENCES training_events(training_id) ON DELETE CASCADE,
  completed_at        DATE,
  result              TEXT,
  PRIMARY KEY (staff_id, training_id)
);

CREATE TABLE competencies (
  competency_id       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  code                TEXT UNIQUE NOT NULL,
  name                TEXT NOT NULL,
  description         TEXT
);

CREATE TABLE staff_competency_assessments (
  assessment_id       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  staff_id            UUID NOT NULL REFERENCES staff_profiles(staff_id) ON DELETE CASCADE,
  competency_id       UUID NOT NULL REFERENCES competencies(competency_id),
  assessed_at         DATE NOT NULL,
  assessor_user_id    UUID REFERENCES users(user_id),
  outcome             TEXT NOT NULL CHECK (outcome IN ('PASS','FAIL','CONDITIONAL')),
  valid_until         DATE,
  evidence_json       JSONB NOT NULL DEFAULT '{}'::jsonb
);

-- =========================
-- 6) EQUIPOS
-- =========================
CREATE TABLE equipment (
  equipment_id        UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  asset_tag           TEXT UNIQUE,
  name                TEXT NOT NULL,
  manufacturer        TEXT,
  model               TEXT,
  serial_number       TEXT,
  location            TEXT,
  status              TEXT NOT NULL CHECK (status IN ('ACTIVE','OUT_OF_SERVICE','RETIRED')),
  installed_at        DATE,
  notes               TEXT
);

CREATE TABLE maintenance_plans (
  plan_id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  equipment_id        UUID NOT NULL REFERENCES equipment(equipment_id) ON DELETE CASCADE,
  plan_name           TEXT NOT NULL,
  frequency_days      INT NOT NULL,
  checklist_json      JSONB NOT NULL DEFAULT '{}'::jsonb,
  is_active           BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE maintenance_events (
  maintenance_event_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  equipment_id         UUID NOT NULL REFERENCES equipment(equipment_id),
  plan_id              UUID REFERENCES maintenance_plans(plan_id),
  performed_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
  performed_by_user_id UUID REFERENCES users(user_id),
  event_type           TEXT NOT NULL CHECK (event_type IN ('PREVENTIVE','CORRECTIVE','INSPECTION')),
  outcome              TEXT,
  notes               TEXT,
  evidence_doc_id      UUID REFERENCES documents(document_id)
);

-- =========================
-- 7) INVENTARIO
-- =========================
CREATE TABLE suppliers (
  supplier_id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name                TEXT UNIQUE NOT NULL,
  contact_name        TEXT,
  email               TEXT,
  phone               TEXT,
  notes               TEXT
);

CREATE TABLE storage_locations (
  location_id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name                TEXT UNIQUE NOT NULL,
  description         TEXT
);

CREATE TABLE reagents (
  reagent_id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name                TEXT NOT NULL,
  manufacturer        TEXT,
  supplier_id         UUID REFERENCES suppliers(supplier_id),
  manufacturer_code   TEXT,
  internal_code       TEXT,
  reagent_type        TEXT NOT NULL,
  unit                TEXT NOT NULL,
  presentation_json   JSONB NOT NULL DEFAULT '{}'::jsonb,
  storage_conditions  TEXT,
  default_location_id UUID REFERENCES storage_locations(location_id),
  open_shelf_life_days INT,
  status              TEXT NOT NULL CHECK (status IN ('ACTIVE','PHASING_OUT','OBSOLETE','BLOCKED')) DEFAULT 'ACTIVE',
  min_stock           NUMERIC(14,3) NOT NULL DEFAULT 0,
  target_stock        NUMERIC(14,3) NOT NULL DEFAULT 0,
  reorder_qty         NUMERIC(14,3) NOT NULL DEFAULT 0,
  substitute_reagent_id UUID REFERENCES reagents(reagent_id),
  replaced_by_reagent_id UUID REFERENCES reagents(reagent_id),
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (manufacturer, manufacturer_code)
);

CREATE TABLE reagent_lots (
  reagent_lot_id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  reagent_id          UUID NOT NULL REFERENCES reagents(reagent_id) ON DELETE CASCADE,
  lot_number          TEXT NOT NULL,
  expiry_date         DATE NOT NULL,
  received_date       DATE NOT NULL,
  received_qty        NUMERIC(14,3) NOT NULL,
  available_qty       NUMERIC(14,3) NOT NULL,
  location_id         UUID REFERENCES storage_locations(location_id),
  status              TEXT NOT NULL CHECK (status IN ('QUARANTINE','RELEASED','BLOCKED','CONSUMED','EXPIRED','RECALLED')) DEFAULT 'RELEASED',
  opened_date         DATE,
  open_expiry_date    DATE,
  release_by_user_id  UUID REFERENCES users(user_id),
  release_at          TIMESTAMPTZ,
  attachments_json    JSONB NOT NULL DEFAULT '{}'::jsonb,
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (reagent_id, lot_number)
);

CREATE TABLE inventory_movements (
  movement_id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  moved_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
  user_id             UUID REFERENCES users(user_id),
  reagent_id          UUID NOT NULL REFERENCES reagents(reagent_id),
  reagent_lot_id      UUID REFERENCES reagent_lots(reagent_lot_id),
  movement_type       TEXT NOT NULL CHECK (movement_type IN ('IN','OUT','ADJUST','WASTE','TRANSFER','RETURN')),
  qty                 NUMERIC(14,3) NOT NULL,
  reason              TEXT NOT NULL,
  reference_type      TEXT,
  reference_id        UUID,
  notes               TEXT
);

-- =========================
-- 8) MEJORA Y CALIDAD
-- =========================
CREATE TABLE nonconformities (
  nc_id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  detected_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
  detected_by_user_id UUID REFERENCES users(user_id),
  title               TEXT NOT NULL,
  description         TEXT NOT NULL,
  severity            TEXT CHECK (severity IN ('LOW','MEDIUM','HIGH','CRITICAL')),
  impact_patient      BOOLEAN NOT NULL DEFAULT FALSE,
  containment         TEXT,
  status              TEXT NOT NULL CHECK (status IN ('OPEN','INVESTIGATING','ACTION','CLOSED')) DEFAULT 'OPEN'
);

CREATE TABLE capa_actions (
  capa_id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  nc_id               UUID REFERENCES nonconformities(nc_id) ON DELETE SET NULL,
  action_type         TEXT NOT NULL CHECK (action_type IN ('CORRECTIVE','PREVENTIVE')),
  description         TEXT NOT NULL,
  owner_user_id       UUID REFERENCES users(user_id),
  due_date            DATE,
  completed_at        DATE,
  effectiveness_check TEXT,
  status              TEXT NOT NULL CHECK (status IN ('OPEN','DONE','VERIFIED','CANCELLED')) DEFAULT 'OPEN'
);
