using System;
using Volo.Abp.Auditing;

namespace NoxStock.Web.Diagnostics;

public sealed class SensitiveDataAuditLogContributor : AuditLogContributor
{
    public override void PostContribute(AuditLogContributionContext context)
    {
        var auditInfo = context.AuditInfo;
        auditInfo.Url = SensitiveDataSanitizer.SanitizeUrl(auditInfo.Url);

        foreach (var action in auditInfo.Actions)
        {
            action.Parameters = SensitiveDataSanitizer.Sanitize(action.Parameters) ?? string.Empty;
        }

        for (var index = 0; index < auditInfo.Exceptions.Count; index++)
        {
            var exception = auditInfo.Exceptions[index];
            var safeMessage = SensitiveDataSanitizer.Sanitize(exception.Message, 500) ?? "No message";
            auditInfo.Exceptions[index] = new Exception($"{exception.GetType().Name}: {safeMessage}");
        }

        for (var index = 0; index < auditInfo.Comments.Count; index++)
        {
            auditInfo.Comments[index] = SensitiveDataSanitizer.Sanitize(auditInfo.Comments[index]) ?? string.Empty;
        }
    }
}
