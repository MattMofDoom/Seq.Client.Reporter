namespace Seq.Client.Reporter
{
    public enum ExitCodes
    {
        Success = 0,
        NoConfig = 1,
        NoQuery = 2,
        TimeFromInvalid = 3,
        TimeToInvalid = 4,
        NoDataReturned = 5,
        ErrorWritingCsv = 6,
        CsvFolderNotFound = 7,
        QueryError = 8,
        MailError = 9
    }
}