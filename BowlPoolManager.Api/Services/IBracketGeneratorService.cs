using BowlPoolManager.Core.Domain;
using BowlPoolManager.Core.Dtos;

namespace BowlPoolManager.Api.Services
{
    public interface IBracketGeneratorService
    {
        /// <summary>
        /// Generates the 67 HoopsGame shells for a complete NCAA tournament bracket.
        /// Games are linked via NextGameId but have no teams assigned (that is Phase 6).
        /// </summary>
        List<HoopsGame> GenerateBracket(BracketGenerationRequest request);
    }
}
