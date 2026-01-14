-- Seed Roles
INSERT INTO roles (role_name, description) VALUES ('Administrator', 'Full system access');
INSERT INTO roles (role_name, description) VALUES ('Manager', 'Management and oversight');
INSERT INTO roles (role_name, description) VALUES ('Quality', 'Quality assurance and audits');
INSERT INTO roles (role_name, description) VALUES ('Staff', 'Basic documentation access');

-- Seed Admin User (Password: admin123)
-- Hash: JAvlGPq9JyTdtvBO6x2llnRI1+gxwIyPqCKAn3THIKk=
WITH admin_user AS (
  INSERT INTO users (username, full_name, email, password_hash)
  VALUES ('admin', 'System Administrator', 'admin@qmsflowdoc.local', 'JAvlGPq9JyTdtvBO6x2llnRI1+gxwIyPqCKAn3THIKk=')
  RETURNING user_id
)
INSERT INTO user_roles (user_id, role_id)
SELECT user_id, role_id FROM admin_user, roles WHERE role_name = 'Administrator';
