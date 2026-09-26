namespace Gas365.Domain;

public sealed record CylinderSettlementAmounts(decimal Deposit, decimal UsageFee, decimal DamageOrLossCharge,
    decimal Revenue, decimal AppliedDeposit, decimal RefundDue, decimal AmountToCollect);

public static class CylinderSettlement
{
    // Authorization and custody validation belong to the application transaction.
    // This calculation never records a cash refund merely because one is owed.
    public static CylinderSettlementAmounts Calculate(decimal deposit, decimal usageFee, decimal approvedDamageOrLossCharge)
    {
        Money(deposit); Money(usageFee); Money(approvedDamageOrLossCharge);
        var revenue = usageFee + approvedDamageOrLossCharge;
        var applied = Math.Min(deposit, revenue);
        return new(deposit, usageFee, approvedDamageOrLossCharge, revenue, applied, deposit - applied, revenue - applied);
    }

    private static void Money(decimal value)
    {
        if (value < 0 || value > 1000000000m || decimal.Round(value, 2) != value)
            throw new ArgumentException("Amounts must be non-negative, two-place PKR values no greater than 1,000,000,000.");
    }
}
