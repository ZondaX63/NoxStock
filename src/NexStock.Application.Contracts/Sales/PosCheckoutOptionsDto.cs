using System;
using System.Collections.Generic;
using NexStock.Accounts;

namespace NexStock.Sales;

public class PosCheckoutOptionsDto
{
    public List<PosAccountLookupDto> Accounts { get; set; } = new();
    public List<PosCustomerLookupDto> Customers { get; set; } = new();
    public Guid? DefaultCashAccountId { get; set; }
    public Guid? DefaultBankAccountId { get; set; }
}

public class PosAccountLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "TRY";
    public AccountType Type { get; set; }
}

public class PosCustomerLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal CreditLimit { get; set; }
    public string Currency { get; set; } = "TRY";
}
