using System.Threading.Tasks;
using TienLen.Server.DTOs;

namespace TienLen.Server.Services {
    public interface IMatchService {
        Task<bool> RecordMatchResultAsync( MatchResultReportDto report );
    }
}
