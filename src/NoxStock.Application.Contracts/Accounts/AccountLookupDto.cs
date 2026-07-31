using System;

namespace NoxStock.Accounts;

public class AccountLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public string Currency { get; set; } = "TRY";
    public decimal Balance { get; set; }
}
