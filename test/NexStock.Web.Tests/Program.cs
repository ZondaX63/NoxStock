using Microsoft.AspNetCore.Builder;
using NexStock;
using Volo.Abp.AspNetCore.TestBase;

var builder = WebApplication.CreateBuilder();
builder.Environment.ContentRootPath = GetWebProjectContentRootPathHelper.Get("NexStock.Web.csproj"); 
await builder.RunAbpModuleAsync<NexStockWebTestModule>(applicationName: "NexStock.Web");

public partial class Program
{
}
