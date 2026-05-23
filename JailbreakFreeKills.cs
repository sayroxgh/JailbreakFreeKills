using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;

namespace JailbreakFreeKills;

public class JailbreakFreeKills : BasePlugin
{
    public override string ModuleName => "Jailbreak FreeKills";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "Sayrox";

    private static readonly string Prefix = $" {ChatColors.Default}[{ChatColors.Gold}Jailbreak FreeKills{ChatColors.Default}]";
    
    private readonly Dictionary<ulong, DateTime> _cooldowns = new();
    private readonly Dictionary<int, (Vector Position, QAngle Rotation)> _lastDeathLocations = new();

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.PlayerPawn.Value == null) return HookResult.Continue;
        
        if (player.TeamNum == 2)
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn.AbsOrigin != null && pawn.AbsRotation != null)
            {
                _lastDeathLocations[player.Slot] = (
                    new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z),
                    new QAngle(pawn.AbsRotation.X, pawn.AbsRotation.Y, pawn.AbsRotation.Z)
                );
            }
        }
        
        return HookResult.Continue;
    }

    [ConsoleCommand("css_fk")]
    public void OnFreeKillCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;

        bool isAdmin = AdminManager.PlayerHasPermissions(player, "@css/generic");
        bool isCt = player.TeamNum == 3;

        if (!isAdmin && !isCt)
        {
            player.PrintToChat($"{Prefix} Yetkininiz Yok !");
            return;
        }

        if (!isAdmin)
        {
            if (_cooldowns.TryGetValue(player.SteamID, out DateTime lastUsed))
            {
                var timePassed = (DateTime.Now - lastUsed).TotalSeconds;
                if (timePassed < 25.0)
                {
                    int timeLeft = (int)Math.Ceiling(25.0 - timePassed);
                    player.PrintToChat($"{Prefix} Tekrar Fk Kullanabilmeniz İçin Lütfen {timeLeft} Bekleyiniz.");
                    return;
                }
            }
        }

        if (info.ArgCount < 2)
        {
            player.PrintToChat($"{Prefix} Kullanım: css_fk <isim>");
            return;
        }

        string targetName = info.GetArg(1);

        if (targetName.StartsWith("@"))
        {
            player.PrintToChat($"{Prefix} Sadece isim belirterek FK kullanabilirsiniz (@ Gibi Çoğul Kişi Belirtmek yasaktır).");
            return;
        }

        var targets = Utilities.GetPlayers().Where(p => 
            p.IsValid && 
            !p.IsBot && 
            p.PlayerName.Contains(targetName, StringComparison.OrdinalIgnoreCase)
        ).ToList();

        if (targets.Count == 0)
        {
            player.PrintToChat($"{Prefix} Belirtilen isme sahip oyuncu bulunamadı!");
            return;
        }
        
        if (targets.Count > 1)
        {
            player.PrintToChat($"{Prefix} Birden fazla oyuncu bulundu, lütfen daha belirgin bir isim girin.");
            return;
        }

        var target = targets[0];

        if (target.TeamNum != 2)
        {
            player.PrintToChat($"{Prefix} Sadece T takımındaki oyuncuları FKlayabilirsiniz.");
            return;
        }

        if (target.PawnIsAlive)
        {
            player.PrintToChat($"{Prefix} Hedef oyuncu zaten hayatta!");
            return;
        }

        if (!_lastDeathLocations.TryGetValue(target.Slot, out var deathLoc))
        {
            player.PrintToChat($"{Prefix} Oyuncunun son ölüm noktası bulunamadı.");
            return;
        }

        target.Respawn();
        
        Server.NextFrame(() =>
        {
            if (target.IsValid && target.PlayerPawn.Value != null)
            {
                target.PlayerPawn.Value.Teleport(deathLoc.Position, deathLoc.Rotation, Vector.Zero);
            }
        });

        Server.PrintToChatAll($"{Prefix} {ChatColors.Gold}{player.PlayerName} {ChatColors.Default}{target.PlayerName} Adlı Kişiyi FK ladı.");

        if (!isAdmin)
        {
            _cooldowns[player.SteamID] = DateTime.Now;
        }
    }
}
