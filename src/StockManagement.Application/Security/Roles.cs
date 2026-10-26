namespace StockManagement.Application.Security;

public static class Roles
{
    public const string Admin = "Admin";
    public const string WarehouseKeeper = "WarehouseKeeper";
    public const string Manager = "Manager";
    public const string ProcurementStaff = "ProcurementStaff";

    public static readonly string[] All = [Admin, WarehouseKeeper, Manager, ProcurementStaff];
}

public static class Permissions
{
    public const string EmployeesManage = "employees.manage";
    public const string ProductsRead = "products.read";
    public const string ProductsManage = "products.manage";
    public const string StockIn = "stock.in";
    public const string StockOut = "stock.out";
    public const string InventoryCount = "inventory.count";
    public const string StockRead = "stock.read";
    public const string ReportsRead = "reports.read";
    public const string ApprovalsManage = "approvals.manage";
    public const string PurchaseOrderView = "po.view";
    public const string PurchaseOrderCreate = "po.create";
    public const string PurchaseOrderApprove = "po.approve";
    public const string PurchaseOrderCancel = "po.cancel";
    public const string PurchaseOrderApproveOverride = "po.approve.override";
    public const string GoodsReceiptCreate = "grn.create";
    public const string GoodsReceiptReverse = "grn.reverse";
    public const string SupplierManage = "supplier.manage";

    public static readonly string[] All =
    [
        EmployeesManage, ProductsRead, ProductsManage, StockIn, StockOut,
        InventoryCount, StockRead, ReportsRead, ApprovalsManage, PurchaseOrderView,
        PurchaseOrderCreate, PurchaseOrderApprove, PurchaseOrderCancel,
        PurchaseOrderApproveOverride,
        GoodsReceiptCreate, GoodsReceiptReverse, SupplierManage
    ];
}

public static class RolePermissionDefaults
{
    public static IReadOnlyCollection<string> For(string role) => role switch
    {
        Roles.Admin => Permissions.All,
        Roles.WarehouseKeeper => [Permissions.ProductsRead, Permissions.StockIn, Permissions.StockOut,
            Permissions.GoodsReceiptCreate, Permissions.PurchaseOrderView],
        Roles.Manager => [Permissions.ProductsRead, Permissions.StockRead, Permissions.ReportsRead,
            Permissions.ApprovalsManage, Permissions.PurchaseOrderView, Permissions.PurchaseOrderCreate,
            Permissions.PurchaseOrderApprove, Permissions.PurchaseOrderCancel, Permissions.GoodsReceiptCreate,
            Permissions.GoodsReceiptReverse, Permissions.SupplierManage, Permissions.PurchaseOrderApproveOverride],
        Roles.ProcurementStaff => [Permissions.PurchaseOrderView, Permissions.PurchaseOrderCreate, Permissions.SupplierManage],
        _ => []
    };
}
