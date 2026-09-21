# NoxStock

## About this solution

This is a layered startup solution based on [Domain Driven Design (DDD)](https://abp.io/docs/latest/framework/architecture/domain-driven-design) practises. All the fundamental ABP modules are already installed. Check the [Application Startup Template](https://abp.io/docs/latest/solution-templates/layered-web-application) documentation for more info.

## Private Beta Prototype

Current beta: `1.0.0-beta.2`

NoxStock is currently prepared for a controlled private beta, not unrestricted production scale. The pilot target is 5-10 active tenants, limited users, and 5,000-10,000 products per tenant.

Supported beta flows include tenant URL login/logout, tenant management by host admin, product creation and OEM search, dynamic TRY/USD/EUR pricing, POS sale and cancellation, sales invoice approval, payment/collection, account and current-account movement checks, dashboard, TR/EN switching, and tenant isolation guards.

Out of scope for this beta are Warehouse/Branch/WarehouseStock rollout, stock reservation, e-Invoice/e-Archive, CRM automation, advanced accounting, broad report refactors, and 100,000-product or 100-300-concurrent-user claims.

Before opening a pilot tenant, follow the release runbook in [`docs/release/PRIVATE_BETA_RELEASE.md`](docs/release/PRIVATE_BETA_RELEASE.md). Production secrets must be injected at runtime and previously exposed credentials must be rotated.

### Pre-requirements

* [.NET10.0+ SDK](https://dotnet.microsoft.com/download/dotnet)
* [Node v18 or 20](https://nodejs.org/en)

### Configurations

The solution comes with a default configuration that works out of the box. However, you may consider to change the following configuration before running your solution:

* Check the `ConnectionStrings` in `appsettings.json` files under the `NoxStock.Web` and `NoxStock.DbMigrator` projects and change it if you need.

### Before running the application

* Run `abp install-libs` command on your solution folder to install client-side package dependencies. This step is automatically done when you create a new solution, if you didn't especially disabled it. However, you should run it yourself if you have first cloned this solution from your source control, or added a new client-side package dependency to your solution.
* Run `NoxStock.DbMigrator` to create the initial database. This step is also automatically done when you create a new solution, if you didn't especially disabled it. This should be done in the first run. It is also needed if a new database migration is added to the solution later.

#### Generating a Signing Certificate

In the production environment, you need to use a production signing certificate. ABP Framework sets up signing and encryption certificates in your application and expects an `openiddict.pfx` file in your application.

To generate a signing certificate, you can use the following command:

```bash
: "${NOXSTOCK_CERTIFICATE_PASSPHRASE:?Set NOXSTOCK_CERTIFICATE_PASSPHRASE first}"
dotnet dev-certs https -v -ep openiddict.pfx -p "$NOXSTOCK_CERTIFICATE_PASSPHRASE"
```

Keep the passphrase in a local or platform secret source. Do not commit it to the repository.

It is recommended to use **two** RSA certificates, distinct from the certificate(s) used for HTTPS: one for encryption, one for signing.

For more information, please refer to: [OpenIddict Certificate Configuration](https://documentation.openiddict.com/configuration/encryption-and-signing-credentials.html#registering-a-certificate-recommended-for-production-ready-scenarios)

> Also, see the [Configuring OpenIddict](https://abp.io/docs/latest/Deployment/Configuring-OpenIddict#production-environment) documentation for more information.

### Solution structure

This is a layered monolith application that consists of the following applications:

* `NoxStock.DbMigrator`: A console application which applies the migrations and also seeds the initial data. It is useful on development as well as on production environment.
* `NoxStock.Web`: ASP.NET Core MVC / Razor Pages application that is the essential web application of the solution.

#### Test Projects

The `test` folder contains the following test projects:

* `NoxStock.Application.Tests`: Application layer tests.
* `NoxStock.Domain.Tests`: Domain layer tests.
* `NoxStock.EntityFrameworkCore.Tests`: Entity Framework Core integration tests.



## Deploying the application

Deploying an ABP application follows the same process as deploying any .NET or ASP.NET Core application. However, there are important considerations to keep in mind. For detailed guidance, refer to ABP's [deployment documentation](https://abp.io/docs/latest/Deployment/Index).

### Additional resources

You can see the following resources to learn more about your solution and the ABP Framework:

* [Application Startup Template](https://abp.io/docs/latest/startup-templates/application/index)



# 1. authorized_keys dosyasına yeni anahtarı ekle
# (Tırnakların arasına biraz önce kopyaladığın ssh-ed25519 ile başlayan metni yapıştır)
echo "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAINESAAvgkgtD29QlK+fvam11qdBRc+U57QmPuhJvOHkV oracle-stockapp-key" | sudo tee -a /mnt/recovery/home/ubuntu/.ssh/authorized_keys

# 2. Dosya izinlerini ve dosya sahipliğini Ubuntu standartlarına göre sabitle
sudo chmod 600 /mnt/recovery/home/ubuntu/.ssh/authorized_keys
sudo chmod 700 /mnt/recovery/home/ubuntu/.ssh
sudo chown -R 1001:1001 /mnt/recovery/home/ubuntu/.ssh

# 3. Anahtarın dosyanın sonuna doğru eklendiğini teyit et
sudo tail -n 1 /mnt/recovery/home/ubuntu/.ssh/authorized_keys