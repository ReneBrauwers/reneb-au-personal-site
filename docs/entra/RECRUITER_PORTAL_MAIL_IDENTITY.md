# Recruiter portal mail identity

This runbook provisions the certificate-only Microsoft identity used to send neutral portal notifications from a dedicated Microsoft 365 mailbox. It contains no production secret values. Substitute the placeholders in an administrative session and never paste private keys, client secrets or certificate bundles into tickets, source control or shell history.

## Target state

- A single-tenant Entra app named `reneb.au Recruiter Portal Mail`.
- A service principal for that app.
- One public certificate credential in Entra; its private key exists only on the production host.
- No organization-wide Microsoft Graph application permissions on the app.
- Exchange Online `Application Mail.Send` assigned through a custom resource scope matching only the dedicated sender mailbox.
- `Test-ServicePrincipalAuthorization` returns `InScope=True` for the sender and `InScope=False` for a different mailbox.

Microsoft documents Exchange Application RBAC and Entra application permissions as additive. Do not grant Graph `Mail.Send` in Entra as well as the scoped Exchange role.

## Prerequisites

- A dedicated Exchange Online mailbox such as `<sender@example.com>`.
- An account able to create Entra applications and service principals.
- Exchange Online `Organization Management` plus the required Entra Exchange Administrator authority.
- Azure CLI and ExchangeOnlineManagement PowerShell.
- A host-only PEM certificate and private key. Generate them as described in `deploy/README.md`; upload only the public certificate.

## Option A: Entra portal / ClickOps

1. Open **Microsoft Entra admin center → Identity → Applications → App registrations → New registration**.
2. Name the app `reneb.au Recruiter Portal Mail`.
3. Select **Accounts in this organizational directory only** and leave Redirect URI empty.
4. Record the Application (client) ID and Directory (tenant) ID.
5. Open **Certificates & secrets → Certificates → Upload certificate** and upload only the public PEM/CRT certificate.
6. Open **API permissions** and confirm there is no Microsoft Graph application `Mail.Send` permission and no other unneeded permission in the app manifest.
7. Open **Enterprise applications**, locate the same application and record its service-principal Object ID. Do not use the app-registration Object ID in the Exchange command. Inspect its **Permissions** view as well; the enterprise application must have no Entra application grant. The CLI proof below remains mandatory because the manifest alone is not authoritative for granted roles.
8. Continue with **Exchange Online Application RBAC** below. Exchange currently requires PowerShell for these scoped assignments; there is no equivalent Azure CLI command.

## Option B: Azure CLI

Use task-specific variables; do not reuse shell/system variables.

```powershell
$portalAppName = 'reneb.au Recruiter Portal Mail'
$portalPublicCert = 'C:\secure-temporary-path\graph-mail-certificate.pem'

$portalAppId = az ad app create `
  --display-name $portalAppName `
  --sign-in-audience AzureADMyOrg `
  --query appId -o tsv

az ad sp create --id $portalAppId --only-show-errors | Out-Null

az ad app credential reset `
  --id $portalAppId `
  --cert "@$portalPublicCert" `
  --append `
  --display-name 'production-host-certificate' `
  --years 2 `
  --only-show-errors | Out-Null

$portalServicePrincipalObjectId = az ad sp show --id $portalAppId --query id -o tsv
$portalTenantId = az account show --query tenantId -o tsv
```

`--append` is mandatory during creation and rotation because the default credential-reset behaviour removes existing credentials. The certificate file passed to Azure CLI must contain only the public certificate.

Verify the result without displaying credential material:

```powershell
az ad app show --id $portalAppId `
  --query '{appId:appId,displayName:displayName,requiredResourceAccess:requiredResourceAccess}' -o json

az ad app credential list --id $portalAppId --cert `
  --query '[].{displayName:displayName,start:startDateTime,end:endDateTime,keyId:keyId}' -o table

az ad sp show --id $portalAppId `
  --query '{appId:appId,id:id,displayName:displayName,accountEnabled:accountEnabled}' -o json

$portalAppRoleAssignments = @(
  az rest --method get `
    --url "https://graph.microsoft.com/v1.0/servicePrincipals/$portalServicePrincipalObjectId/appRoleAssignments" `
    --query value -o json | ConvertFrom-Json
)
if ($portalAppRoleAssignments.Count -ne 0) {
  throw 'The dedicated service principal has an Entra application-role grant; inspect and remove it before continuing.'
}
```

`requiredResourceAccess` must be empty for this dedicated app, and the enterprise service principal's `appRoleAssignments` collection must also be empty. The former is the app's requested-permission manifest; the latter is the authoritative granted application-role surface. Do not run `az ad app permission add` or `az ad app permission admin-consent` for Graph `Mail.Send`.

## Exchange Online Application RBAC

Connect interactively with an appropriately authorized administrator:

```powershell
Import-Module ExchangeOnlineManagement
Connect-ExchangeOnline -UserPrincipalName '<exchange-admin@example.com>' -ShowBanner:$false
```

Create an Exchange pointer to the Entra service principal, the one-mailbox resource scope and the scoped role assignment. Use the **service-principal Object ID**, not the app-registration Object ID.

```powershell
$portalAppId = '<application-client-id>'
$portalServicePrincipalObjectId = '<enterprise-app-object-id>'
$portalSenderMailbox = '<sender@example.com>'
$portalScopeName = 'reneb-au-recruiter-portal-sender'
$portalAssignmentName = 'reneb-au-recruiter-portal-mail-send'

New-ServicePrincipal `
  -AppId $portalAppId `
  -ObjectId $portalServicePrincipalObjectId `
  -DisplayName 'reneb.au Recruiter Portal Mail'

New-ManagementScope `
  -Name $portalScopeName `
  -RecipientRestrictionFilter "PrimarySmtpAddress -eq '$portalSenderMailbox'"

New-ManagementRoleAssignment `
  -Name $portalAssignmentName `
  -App $portalServicePrincipalObjectId `
  -Role 'Application Mail.Send' `
  -CustomResourceScope $portalScopeName
```

For reruns, inventory `Get-ServicePrincipal`, `Get-ManagementScope` and `Get-ManagementRoleAssignment` first and update only the object that differs. Do not create duplicate assignments.

## Mandatory authorization proof

```powershell
$portalAllowed = Test-ServicePrincipalAuthorization `
  -Identity $portalServicePrincipalObjectId `
  -Resource $portalSenderMailbox

$portalDenied = Test-ServicePrincipalAuthorization `
  -Identity $portalServicePrincipalObjectId `
  -Resource '<different-mailbox@example.com>'

$portalAllowed | Format-Table RoleName,GrantedPermissions,AllowedResourceScope,InScope
$portalDenied | Format-Table RoleName,GrantedPermissions,AllowedResourceScope,InScope
```

Acceptance requires `Application Mail.Send` with `InScope=True` for the sender and `InScope=False` for a genuinely different mailbox. An alias of the sender is not a valid negative test because it resolves to the same mailbox.

Also confirm both the Entra app manifest and the service principal's granted-role collection are empty:

```powershell
az ad app show --id $portalAppId --query requiredResourceAccess -o json

$portalGrantedRoles = @(
  az rest --method get `
    --url "https://graph.microsoft.com/v1.0/servicePrincipals/$portalServicePrincipalObjectId/appRoleAssignments" `
    --query value -o json | ConvertFrom-Json
)
if ($portalGrantedRoles.Count -ne 0) {
  throw 'Unscoped Entra application-role grant detected.'
}
```

After propagation, acquire a certificate client-credentials token and test Graph `POST /users/<sender>/sendMail`. A `202 Accepted` proves acceptance by Graph, not final delivery; confirm delivery to an external mailbox and confirm a send attempt as the different mailbox is denied.

## Production configuration

Store only identifiers and policy lists in the host-owned `.env`:

```dotenv
ADMIN_EMAILS=admin@example.com
MAIL_TENANT_ID=<tenant-id>
MAIL_CLIENT_ID=<application-client-id>
MAIL_SENDER_MAILBOX=<sender@example.com>
```

Mount the public certificate and private key using `GRAPH_MAIL_CERTIFICATE_FILE` and `GRAPH_MAIL_PRIVATE_KEY_FILE`. Keep the private key outside `.env`, source control, container layers and support bundles.

## Certificate rotation

1. Generate a new key pair on the production host without replacing the old files.
2. Upload the new public certificate with `az ad app credential reset --append`.
3. Mount the new host files and recreate the portal while discovery is disabled.
4. Prove token acquisition, scoped authorization and external delivery.
5. Retain the old certificate until rollback is no longer required.
6. Delete only the old credential by its `keyId` after acceptance.

If the new credential fails, restore the old file paths and recreate the portal. Do not remove the old Entra credential during the rollback window.

## Deprovisioning

Disable discovery first. Remove the Exchange role assignment, then its custom scope and Exchange service-principal pointer. Delete the Entra service principal/application only after proving no other workload uses it. Remove host keys last, after backup and rollback retention requirements have expired.

## References

- [Microsoft Graph sendMail](https://learn.microsoft.com/en-us/graph/api/user-sendmail?view=graph-rest-1.0)
- [Exchange Online Application RBAC](https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac)
- [Azure CLI application certificate credentials](https://learn.microsoft.com/en-us/cli/azure/ad/app/credential?view=azure-cli-latest)
