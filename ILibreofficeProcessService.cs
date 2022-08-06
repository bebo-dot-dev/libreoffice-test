namespace libreoffice_test;

public interface ILibreofficeProcessService
{
    Task ConvertFile(int retryCount = 0, string lastError = "");
}