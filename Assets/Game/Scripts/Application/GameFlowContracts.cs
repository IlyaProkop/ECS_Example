using System;
using Game.Domain;

namespace Game.Flow {
    // Commands exposed to UI models; scene attachment and run ownership stay with the host.
    public interface IGameCommands {
        bool Play();
        bool Restart();
        bool ReturnToMenu();
    }

    // The run owns everything that must end before another battle starts.
    public interface IBattleRun : IDisposable {
        void Tick(float deltaTime);
        bool TryGetResult(out BattleResult result);
    }

    public interface IBattleFactory {
        // Ownership transfers to the controller on return. Clean partial resources if creation throws.
        IBattleRun Create();
    }

    public interface IGameNavigation {
        // The scene may attach during this call or after it returns.
        void OpenBattle();
        void OpenMenu();
    }

    public interface IResultView {
        void ShowResult(in BattleResult result, PlayerProgress progress, string saveStatus);
    }

    public interface IProgressService {
        PlayerProgress Current { get; }
        string Status { get; }
        void Record(in BattleResult result);
        void Flush();
    }
}
