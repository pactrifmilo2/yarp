# Deploy ATFM Gateway with NSSM

This deployment runs the Gateway and Admin applications on the same Windows PC:

- Gateway: `http://0.0.0.0:5176`
- Admin: `http://0.0.0.0:5106`
- Admin reloads Gateway through `http://127.0.0.1:5176`

Both applications must use the same PostgreSQL database.

## 1. Publish

Run from the repository root:

```powershell
dotnet publish .\MyProxy\MyProxy.csproj --configuration Release --output C:\Services\MyProxy\Gateway
dotnet publish .\MyProxy.Admin\MyProxy.Admin.csproj --configuration Release --output C:\Services\MyProxy\Admin
```

Install the matching .NET runtime on the deployment PC. For a deployment that bundles the runtime, add `--runtime win-x64 --self-contained true` to both commands.

## 2. Configure production settings

The committed `appsettings.json` files contain the current internal defaults. Prefer overriding secrets through NSSM environment variables:

```text
ConnectionStrings__GatewayDatabase=Host=DATABASE_HOST;Port=5432;Database=myproxy_gateway;Username=postgres;Password=REPLACE_ME
```

Set this variable to the same value for both services.

The Admin service also requires:

```text
Gateway__BaseUrl=http://127.0.0.1:5176
```

## 3. Install the Gateway service

Run an elevated terminal:

```powershell
nssm install MyProxyGateway "C:\Program Files\dotnet\dotnet.exe" "C:\Services\MyProxy\Gateway\MyProxy.dll"
nssm set MyProxyGateway AppDirectory "C:\Services\MyProxy\Gateway"
nssm set MyProxyGateway Start SERVICE_AUTO_START
nssm set MyProxyGateway AppExit Default Restart
nssm set MyProxyGateway AppRestartDelay 5000
nssm set MyProxyGateway AppStdout "C:\Services\MyProxy\Logs\gateway.log"
nssm set MyProxyGateway AppStderr "C:\Services\MyProxy\Logs\gateway-error.log"
```

Set the database connection in the NSSM Environment tab or with `AppEnvironmentExtra`.

## 4. Install the Admin service

```powershell
nssm install MyProxyAdmin "C:\Program Files\dotnet\dotnet.exe" "C:\Services\MyProxy\Admin\MyProxy.Admin.dll"
nssm set MyProxyAdmin AppDirectory "C:\Services\MyProxy\Admin"
nssm set MyProxyAdmin DependOnService MyProxyGateway
nssm set MyProxyAdmin Start SERVICE_AUTO_START
nssm set MyProxyAdmin AppExit Default Restart
nssm set MyProxyAdmin AppRestartDelay 5000
nssm set MyProxyAdmin AppStdout "C:\Services\MyProxy\Logs\admin.log"
nssm set MyProxyAdmin AppStderr "C:\Services\MyProxy\Logs\admin-error.log"
```

Set the database connection and `Gateway__BaseUrl` in the NSSM Environment tab.

Create `C:\Services\MyProxy\Logs` before starting the services.

## 5. Start and verify

```powershell
nssm start MyProxyGateway
nssm start MyProxyAdmin
```

Verify locally:

```powershell
Invoke-RestMethod http://127.0.0.1:5176/health
Invoke-RestMethod http://127.0.0.1:5106/health
```

Remote users access:

```text
Gateway:    http://SERVER_IP:5176/
Monitoring: http://SERVER_IP:5106/monitoring
```

Allow inbound TCP `5176` for API clients. Restrict TCP `5106` to administrators because the Admin application does not yet have login authentication.
