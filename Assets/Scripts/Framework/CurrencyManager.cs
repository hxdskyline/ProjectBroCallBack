using System;

public class CurrencyManager
{
    private readonly ICurrencyStorage _storage;

    public CurrencyManager(ICurrencyStorage storage)
    {
        _storage = storage;
    }

    public string SaveId => _storage != null ? _storage.SaveId : string.Empty;

    public long GetCurrencyAmount(CurrencyType currencyType)
    {
        return GetCurrencyAmount(GetCurrencyKey(currencyType));
    }

    public long GetCurrencyAmount(string currencyId)
    {
        if (_storage == null || string.IsNullOrEmpty(currencyId))
        {
            return 0;
        }

        return _storage.GetCurrencyAmount(currencyId);
    }

    public void SetCurrencyAmount(CurrencyType currencyType, long amount, bool saveImmediately = true)
    {
        SetCurrencyAmount(GetCurrencyKey(currencyType), amount, saveImmediately);
    }

    public void SetCurrencyAmount(string currencyId, long amount, bool saveImmediately = true)
    {
        if (_storage == null || string.IsNullOrEmpty(currencyId))
        {
            return;
        }

        _storage.SetCurrencyAmount(currencyId, Math.Max(0L, amount), saveImmediately);
    }

    public long AddCurrency(CurrencyType currencyType, long amount, bool saveImmediately = true)
    {
        return AddCurrency(GetCurrencyKey(currencyType), amount, saveImmediately);
    }

    public long AddCurrency(string currencyId, long amount, bool saveImmediately = true)
    {
        long nextAmount = GetCurrencyAmount(currencyId) + amount;
        if (nextAmount < 0)
        {
            nextAmount = 0;
        }

        SetCurrencyAmount(currencyId, nextAmount, saveImmediately);
        return nextAmount;
    }

    public bool TrySpendCurrency(CurrencyType currencyType, long amount, bool saveImmediately = true)
    {
        return TrySpendCurrency(GetCurrencyKey(currencyType), amount, saveImmediately);
    }

    public bool TrySpendCurrency(string currencyId, long amount, bool saveImmediately = true)
    {
        if (amount <= 0)
        {
            return true;
        }

        long currentAmount = GetCurrencyAmount(currencyId);
        if (currentAmount < amount)
        {
            return false;
        }

        SetCurrencyAmount(currencyId, currentAmount - amount, saveImmediately);
        return true;
    }

    public void Save()
    {
        if (_storage == null)
        {
            return;
        }

        _storage.SaveCurrencyData();
    }

    public static string GetCurrencyKey(CurrencyType currencyType)
    {
        switch (currencyType)
        {
            case CurrencyType.Gold:
                return "gold";
            case CurrencyType.Diamond:
                return "diamond";
            default:
                return currencyType.ToString().ToLowerInvariant();
        }
    }
}