using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using Services.Models.StatisticModels;

namespace SHOCA.API.Controllers
{
    [Route("api/v1/statistics")]
    [ApiController]
    public class StatisticsController : ControllerBase
    {
        private readonly IStatisticsService _statisticsService;

        public StatisticsController(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService ?? throw new ArgumentNullException(nameof(statisticsService));
        }

        [HttpGet]
        public async Task<ActionResult<StatisticsModel>> GetStatistics()
        {
            try
            {
                var statistics = await _statisticsService.GetStatisticsAsync();
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = false, Message = $"Lỗi khi lấy thống kê: {ex.Message}" });
            }
        }
    }
}
