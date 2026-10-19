using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StockManagement.Infrastructure.Persistence;

#nullable disable

namespace StockManagement.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(StockManagementDbContext))]
    [Migration("20261016130000_AddProductBatches")]
    partial class AddProductBatches
    {
    }
}
