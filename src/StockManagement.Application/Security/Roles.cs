namespace StockManagement.Application.Security;

public static class Roles
{
    public const string Admin = "Admin";
    public const string WarehouseKeeper = "WarehouseKeeper";
    public const string Manager = "Manager";

    public static readonly string[] All = [Admin, WarehouseKeeper, Manager];
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

    public static readonly string[] All =
    [
        EmployeesManage, ProductsRead, ProductsManage, StockIn, StockOut,
        InventoryCount, StockRead, ReportsRead, ApprovalsManage
    ];
}
