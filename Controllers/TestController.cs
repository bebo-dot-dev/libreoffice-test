using Microsoft.AspNetCore.Mvc;

namespace libreoffice_test.Controllers;

[ApiController]
[Route("[controller]")]
public class TestController : ControllerBase
{
    private readonly ILibreofficeProcessService _libreofficeProcessService;
    
    public TestController(ILibreofficeProcessService libreofficeProcessService)
    {
        _libreofficeProcessService = libreofficeProcessService;
    }
    
    [HttpGet]
    public async Task<string> Get()
    {
        await _libreofficeProcessService.ConvertFile();
        return "converted";
    }
}