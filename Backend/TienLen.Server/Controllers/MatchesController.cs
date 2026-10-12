using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TienLen.Server.DTOs;
using TienLen.Server.Services;

namespace TienLen.Server.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class MatchesController : ControllerBase {
        private readonly IMatchService matchService;

        public MatchesController( IMatchService matchService ) {
            this.matchService = matchService;
        }

        [HttpPost("report")]
        public async Task<IActionResult> ReportMatch( [FromBody] MatchResultReportDto request ) {
            if ( request == null || request.Results == null || request.Results.Count == 0 ) {
                return BadRequest(ApiResponse<bool>.Fail("Invalid match report payload."));
            }

            bool result = await matchService.RecordMatchResultAsync(request);
            if ( !result ) {
                return BadRequest(ApiResponse<bool>.Fail("Failed to record match result."));
            }

            return Ok(ApiResponse<bool>.Success(true, "Match recorded successfully."));
        }
    }
}
