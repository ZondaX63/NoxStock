using Microsoft.AspNetCore.Builder;
using NoxStock;
using Volo.Abp.AspNetCore.TestBase;

var builder = WebApplication.CreateBuilder();
builder.Environment.ContentRootPath = GetWebProjectContentRootPathHelper.Get("NoxStock.Web.csproj"); 
await builder.RunAbpModuleAsync<NoxStockWebTestModule>(applicationName: "NoxStock.Web");

public partial class Program
{
}
