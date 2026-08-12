namespace EnquirySort.Api.Enums;

public enum SqlQueryResult
{
    UnknownError = 0,
    Ok = 1,
    RecordDidNotExist = 2,
    RecordAlreadyExists = 3,
    ConcurrencyKeyInvalid = 4
}
