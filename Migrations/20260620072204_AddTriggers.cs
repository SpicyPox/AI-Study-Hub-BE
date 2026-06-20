using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIStudyHub.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Hàm 1: Tự động update cột updated_at
CREATE OR REPLACE FUNCTION ai_study_hub.update_modified_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Hàm 2: Tự cấp ví lưu trữ khi User Đăng ký
CREATE OR REPLACE FUNCTION ai_study_hub.create_user_storage_on_signup()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO ai_study_hub.user_storage (user_id) VALUES (NEW.id);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Hàm 3: Xử lý cộng/trừ dung lượng VÀ gói dịch vụ khi thanh toán thành công (Đã tối ưu hóa)
CREATE OR REPLACE FUNCTION ai_study_hub.update_storage_and_sub_on_payment_success()
RETURNS TRIGGER AS $$
DECLARE
    v_duration_days INT;
    v_base_storage BIGINT;
BEGIN
    -- Khi hóa đơn chuyển từ trạng thái khác sang 'completed'
    IF NEW.status = 'completed' AND OLD.status != 'completed' THEN
        
        -- TH1: Nếu mua gói dung lượng lưu trữ lẻ
        IF NEW.purchase_kind = 'storage_package' THEN
            UPDATE ai_study_hub.user_storage 
            SET total_capacity_bytes = total_capacity_bytes + NEW.storage_added_bytes 
            WHERE user_id = NEW.user_id;
            
        -- TH2: Nếu mua gói thành viên sử dụng định kỳ (Subscription)
        ELSIF NEW.purchase_kind = 'subscription_package' THEN
            SELECT duration_days, base_storage_bytes INTO v_duration_days, v_base_storage
            FROM ai_study_hub.subscription_packages WHERE id = NEW.subscription_package_id;
            
            -- Hủy kích hoạt gói cũ
            UPDATE ai_study_hub.user_subscriptions 
            SET status = 'expired' 
            WHERE user_id = NEW.user_id AND status = 'active';
            
            -- Tạo gói mới cho User
            INSERT INTO ai_study_hub.user_subscriptions (user_id, package_id, start_date, end_date, status)
            VALUES (NEW.user_id, NEW.subscription_package_id, NOW(), NOW() + (v_duration_days || ' days')::INTERVAL, 'active');
            
            -- Tăng dung lượng tương ứng đi kèm gói thành viên
            UPDATE ai_study_hub.user_storage 
            SET total_capacity_bytes = total_capacity_bytes + v_base_storage
            WHERE user_id = NEW.user_id;
        END IF;
        
    -- Khi hóa đơn bị 'refunded' (Hoàn tiền)
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

-- Hàm 4: Tự động tính toán used_bytes cực kỳ chính xác (Đã vá lỗi logic rò rỉ dung lượng)
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
        -- Hành động: Ném vào thùng rác (Soft delete)
        IF NEW.is_deleted = TRUE AND OLD.is_deleted = FALSE THEN
            UPDATE ai_study_hub.user_storage SET used_bytes = used_bytes - COALESCE(OLD.file_size, 0) WHERE user_id = NEW.user_id;
        -- Hành động: Khôi phục từ thùng rác
        ELSIF NEW.is_deleted = FALSE AND OLD.is_deleted = TRUE THEN
            UPDATE ai_study_hub.user_storage SET used_bytes = used_bytes + COALESCE(NEW.file_size, 0) WHERE user_id = NEW.user_id;
        -- Hành động: File cập nhật thông thường (ví dụ: đổi file mới nặng hơn/nhẹ hơn) nhưng không đổi trạng thái xóa
        ELSIF NEW.is_deleted = FALSE AND OLD.is_deleted = FALSE THEN
            UPDATE ai_study_hub.user_storage 
            SET used_bytes = used_bytes + (COALESCE(NEW.file_size, 0) - COALESCE(OLD.file_size, 0)) 
            WHERE user_id = NEW.user_id;
        END IF; 
        RETURN NEW;
    END IF;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER update_user_modtime BEFORE UPDATE ON ai_study_hub.users FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();
CREATE TRIGGER update_subject_modtime BEFORE UPDATE ON ai_study_hub.subjects FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();
CREATE TRIGGER update_doc_modtime BEFORE UPDATE ON ai_study_hub.documents FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();
CREATE TRIGGER update_cloud_files_modtime BEFORE UPDATE ON ai_study_hub.cloud_files FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();
CREATE TRIGGER update_chat_modtime BEFORE UPDATE ON ai_study_hub.chat_sessions FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();
CREATE TRIGGER update_user_storage_modtime BEFORE UPDATE ON ai_study_hub.user_storage FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();
CREATE TRIGGER update_transactions_modtime BEFORE UPDATE ON ai_study_hub.transactions FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();
CREATE TRIGGER update_sub_packages_modtime BEFORE UPDATE ON ai_study_hub.subscription_packages FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_modified_column();

CREATE TRIGGER trigger_create_user_storage AFTER INSERT ON ai_study_hub.users FOR EACH ROW EXECUTE FUNCTION ai_study_hub.create_user_storage_on_signup();
CREATE TRIGGER trigger_update_storage_on_payment AFTER UPDATE ON ai_study_hub.transactions FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_storage_and_sub_on_payment_success();
CREATE TRIGGER trigger_update_used_storage AFTER INSERT OR UPDATE OR DELETE ON ai_study_hub.documents FOR EACH ROW EXECUTE FUNCTION ai_study_hub.update_used_storage_on_doc_change();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
