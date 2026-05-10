namespace CariErinc.Helpers;

public class ServiceResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }

    public static ServiceResult Success(string message = "İşlem başarılı.", object? data = null)
    {
        return new ServiceResult { IsSuccess = true, Message = message, Data = data };
    }

    public static ServiceResult Failure(string message, object? data = null)
    {
        return new ServiceResult { IsSuccess = false, Message = message, Data = data };
    }
}

public class ServiceResult<T> : ServiceResult
{
    public T? Value { get; set; }

    public static ServiceResult<T> Success(T value, string message = "İşlem başarılı.")
    {
        return new ServiceResult<T> { IsSuccess = true, Message = message, Value = value };
    }

    public new static ServiceResult<T> Failure(string message, object? data = null)
    {
        return new ServiceResult<T> { IsSuccess = false, Message = message, Data = data };
    }
}
