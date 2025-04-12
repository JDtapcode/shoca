using Services.Models.StatisticModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Interfaces
{
    public interface IStatisticsService
    {
        Task<StatisticsModel> GetStatisticsAsync();
    }
}
