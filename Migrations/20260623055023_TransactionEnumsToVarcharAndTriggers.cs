using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class TransactionEnumsToVarcharAndTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Doi cot truoc khi xoa enum type (xem ghi chu cung loi 2BP01 trong cac migration truoc).
            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "ai_study_hub",
                table: "transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "ai_study_hub.payment_status");

            migrationBuilder.AlterColumn<string>(
                name: "purchase_kind",
                schema: "ai_study_hub",
                table: "transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "method",
                schema: "ai_study_hub",
                table: "transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "ai_study_hub.payment_method");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:ai_study_hub.user_role", "user,admin")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.payment_method", "vnpay,momo,stripe,bank_transfer")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.payment_status", "pending,completed,failed,refunded")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.user_role", "user,admin");

            // Port tu migration "AddTriggers" cua team (nhanh feat/backend) - tu dong cap vi luu tru
            // khi dang ky, tu cong/tru dung luong khi thanh toan thanh cong/hoan tien, tu tinh
            // used_bytes khi upload/xoa/khoi phuc tai lieu. Cac so sanh chuoi nhu NEW.status =
            // 'completed' la literal SQL nen Postgres tu ep kieu duoc, khong gap lai bug 42883.
            migrationBuilder.Sql(@"
-- Ham 1: Tu dong update cot updated_at
CREATE OR REPLACE FUNCTION ai_study_hub.update_modified_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Ham 2: Tu cap vi luu tru khi User dang ky
CREATE OR REPLACE FUNCTION ai_study_hub.create_user_storage_on_signup()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO ai_study_hub.user_storage (user_id) VALUES (NEW.id);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Ham 3: Xu ly cong/tru dung luong VA goi dich vu khi thanh toan thanh cong
CREATE OR REPLACE FUNCTION ai_study_hub.update_storage_and_sub_on_payment_success()
RETURNS TRIGGER AS $$
DECLARE
    v_duration_days INT;
    v_base_storage BIGINT;
BEGIN
    IF NEW.status = 'completed' AND OLD.status != 'completed' THEN
        IF NEW.purchase_kind = 'storage_package' THEN
            UPDATE ai_study_hub.user_storage
            SET total_capacity_bytes = total_capacity_bytes + NEW.storage_added_bytes
            WHERE user_id = NEW.user_id;
        ELSIF NEW.purchase_kind = 'subscription_package' THEN
            SELECT duration_days, base_storage_bytes INTO v_duration_days, v_base_storage
            FROM ai_study_hub.subscription_packages WHERE id = NEW.subscription_package_id;

            UPDATE ai_study_hub.user_subscriptions
            SET status = 'expired'
            WHERE user_id = NEW.user_id AND status = 'active';

            INSERT INTO ai_study_hub.user_subscriptions (user_id, package_id, start_date, end_date, status)
            VALUES (NEW.user_id, NEW.subscription_package_id, NOW(), NOW() + (v_duration_days || ' days')::INTERVAL, 'active');

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
            FROM ai_study_hub.subscription_packages WHERE id = NEW.subscription_package_id;

            UPDATE ai_study_hub.user_subscriptions SET status = 'cancelled' WHERE user_id = NEW.user_id AND package_id = NEW.subscription_package_id AND status = 'active';
            UPDATE ai_study_hub.user_storage SET total_capacity_bytes = total_capacity_bytes - v_base_storage WHERE user_id = NEW.user_id;
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Ham 4: Tu dong tinh toan used_bytes
CREATE OR REPLACE FUNCTION ai_study_hub.update_used_storage_on_doc_change()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        IF COALESCE(NEW.is_deleted, FALSE) = FALSE THEN
            UPDATE ai_study_hub.user_storage SET used_bytes = used_bytes + COALESCE(NEW.file_size, 0) WHERE user_id = NEW.user_id;
        END IF;
        RETURN NEW;
    ELSIF TG_OP = 'DELETE' THEN
        IF COALESCE(OLD.is_deleted, FALSE) = FALSE THEN
            UPDATE ai_study_hub.user_storage SET used_bytes = used_bytes - COALESCE(OLD.file_size, 0) WHERE user_id = OLD.user_id;
        END IF;
        RETURN OLD;
    ELSIF TG_OP = 'UPDATE' THEN
        IF NEW.is_deleted = TRUE AND OLD.is_deleted = FALSE THEN
            UPDATE ai_study_hub.user_storage SET used_bytes = used_bytes - COALESCE(OLD.file_size, 0) WHERE user_id = NEW.user_id;
        ELSIF NEW.is_deleted = FALSE AND OLD.is_deleted = TRUE THEN
            UPDATE ai_study_hub.user_storage SET used_bytes = used_bytes + COALESCE(NEW.file_size, 0) WHERE user_id = NEW.user_id;
        ELSIF NEW.is_deleted = FALSE AND OLD.is_deleted = FALSE THEN
            UPDATE ai_study_hub.user_storage
            SET used_bytes = used_bytes + (COALESCE(NEW.file_size, 0) - COALESCE(OLD.file_size, 0))
            WHERE user_id = NEW.user_id;
        END IF;
        RETURN NEW;
    END IF;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS update_user_modtime ON ai_study_hub.users;
CREATE TRIGGER update_user_modtime BEFORE UPDATE ON ai_study_hub.users FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();
DROP TRIGGER IF EXISTS update_subject_modtime ON ai_study_hub.subjects;
CREATE TRIGGER update_subject_modtime BEFORE UPDATE ON ai_study_hub.subjects FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();
DROP TRIGGER IF EXISTS update_doc_modtime ON ai_study_hub.documents;
CREATE TRIGGER update_doc_modtime BEFORE UPDATE ON ai_study_hub.documents FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();
DROP TRIGGER IF EXISTS update_cloud_files_modtime ON ai_study_hub.cloud_files;
CREATE TRIGGER update_cloud_files_modtime BEFORE UPDATE ON ai_study_hub.cloud_files FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();
DROP TRIGGER IF EXISTS update_chat_modtime ON ai_study_hub.chat_sessions;
CREATE TRIGGER update_chat_modtime BEFORE UPDATE ON ai_study_hub.chat_sessions FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();
DROP TRIGGER IF EXISTS update_user_storage_modtime ON ai_study_hub.user_storage;
CREATE TRIGGER update_user_storage_modtime BEFORE UPDATE ON ai_study_hub.user_storage FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();
DROP TRIGGER IF EXISTS update_transactions_modtime ON ai_study_hub.transactions;
CREATE TRIGGER update_transactions_modtime BEFORE UPDATE ON ai_study_hub.transactions FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();
DROP TRIGGER IF EXISTS update_sub_packages_modtime ON ai_study_hub.subscription_packages;
CREATE TRIGGER update_sub_packages_modtime BEFORE UPDATE ON ai_study_hub.subscription_packages FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();

DROP TRIGGER IF EXISTS trigger_create_user_storage ON ai_study_hub.users;
CREATE TRIGGER trigger_create_user_storage AFTER INSERT ON ai_study_hub.users FOR EACH ROW EXECUTE FUNCTION ai_study_hub.create_user_storage_on_signup();
DROP TRIGGER IF EXISTS trigger_update_storage_on_payment ON ai_study_hub.transactions;
CREATE TRIGGER trigger_update_storage_on_payment AFTER UPDATE ON ai_study_hub.transactions FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_storage_and_sub_on_payment_success();
DROP TRIGGER IF EXISTS trigger_update_used_storage ON ai_study_hub.documents;
CREATE TRIGGER trigger_update_used_storage AFTER INSERT OR UPDATE OR DELETE ON ai_study_hub.documents FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_used_storage_on_doc_change();

-- Backfill user_storage cho cac user da ton tai truoc khi co trigger nay (vi trigger chi chay
-- cho INSERT moi, user cu se khong tu duoc cap vi luu tru).
INSERT INTO ai_study_hub.user_storage (user_id)
SELECT u.id FROM ai_study_hub.users u
LEFT JOIN ai_study_hub.user_storage us ON us.user_id = u.id
WHERE us.user_id IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:ai_study_hub.payment_method", "vnpay,momo,stripe,bank_transfer")
                .Annotation("Npgsql:Enum:ai_study_hub.payment_status", "pending,completed,failed,refunded")
                .Annotation("Npgsql:Enum:ai_study_hub.user_role", "user,admin")
                .OldAnnotation("Npgsql:Enum:ai_study_hub.user_role", "user,admin");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                schema: "ai_study_hub",
                table: "transactions",
                type: "ai_study_hub.payment_status",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "purchase_kind",
                schema: "ai_study_hub",
                table: "transactions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "method",
                schema: "ai_study_hub",
                table: "transactions",
                type: "ai_study_hub.payment_method",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);
        }
    }
}
