using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SimpleProgressApi.Controller;

[ApiController]
[Route("[controller]")]
public class ProgressAController : ControllerBase
{
    private static int progress = 0;
    private static bool jobStarted = false;
    private static bool finished = false;

    [HttpPost("start")]
    public IActionResult StartJob()
    {
        progress = 0;
        jobStarted = true;
        finished = false;
        return Ok(new { started = true });
    }

    [HttpGet("progress")]
    public IActionResult GetProgress()
    {
        if (!jobStarted)
            return Ok(new { started = false, progress = 0, finished = false });

        if (progress < 100)
            progress += 10;

        if (progress >= 100)
        {
            progress = 100;
            jobStarted = false;
            finished = true;
        }
        else
        {
            jobStarted = true;
            finished = false;
        }

        return Ok(new { started = jobStarted, progress, finished });
    }
}
