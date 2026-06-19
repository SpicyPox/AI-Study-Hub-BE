namespace AIStudyHub.Api.Models;

public enum UserRole { user, admin }
public enum DocVisibility { @public, @private }
public enum CloudStatus { pending, uploaded, failed }
public enum ChatRole { user, assistant }
public enum PaymentStatus { pending, completed, failed, refunded }
public enum PaymentMethod { vnpay, momo, stripe, bank_transfer }
public enum PurchaseType { storage_package, subscription_package }
