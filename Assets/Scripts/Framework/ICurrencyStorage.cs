public interface ICurrencyStorage
{
    string SaveId { get; }
    long GetCurrencyAmount(string currencyId);
    void SetCurrencyAmount(string currencyId, long amount, bool saveImmediately);
    void SaveCurrencyData();
}