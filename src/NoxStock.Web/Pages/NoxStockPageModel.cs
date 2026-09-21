using System;
using System.Globalization;
using NoxStock.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace NoxStock.Web.Pages;

public abstract class NoxStockPageModel : AbpPageModel
{
    protected NoxStockPageModel()
    {
        LocalizationResourceType = typeof(NoxStockResource);
    }

    protected bool TryNormalizePostedDecimal(string key, Action<decimal> assign)
    {
        if (!Request.HasFormContentType)
        {
            return true;
        }

        var rawValue = Request.Form[key].ToString();
        ModelState.Remove(key);
        if (TryParsePostedDecimal(rawValue, out var value))
        {
            assign(Math.Round(value, 2, MidpointRounding.AwayFromZero));
            return true;
        }

        ModelState.AddModelError(key, "Lütfen geçerli bir sayısal değer girin.");
        return false;
    }

    private static bool TryParsePostedDecimal(string rawValue, out decimal value)
    {
        if (rawValue.Contains(",", StringComparison.Ordinal) && !rawValue.Contains(".", StringComparison.Ordinal) &&
            decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out value))
        {
            return true;
        }

        return decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
