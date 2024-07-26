using Framework.Constants;

namespace Game.Entities
{
    public class RestMgr
    {
        Player _player;
        ServerTime _restTime;
        int _innAreaTriggerId;
        float[] _restBonus = new float[(int)RestTypes.Max];
        float _XpBonusDeposit;
        RestFlag _restFlagMask;

        public RestMgr(Player player)
        {
            _player = player;
        }

        int GetNextLevelXpBonus() => _player.m_activePlayerData.NextLevelXP;
        int GetNextLevelHonorBonus() => _player.m_activePlayerData.HonorNextLevel;
        float GetRestBonusMax(int nextLevelBonus) => nextLevelBonus * 1.5f / 2;

        public void SetRestBonus(RestTypes restType, float restBonus)
        {
            int next_level_xp;
            bool affectedByRaF = false;

            switch (restType)
            {
                case RestTypes.XP:
                    // Reset restBonus (XP only) for max level players
                    if (_player.GetLevel() >= WorldConfig.Values[WorldCfg.MaxPlayerLevel].Int32)
                        restBonus = 0;

                    next_level_xp = GetNextLevelXpBonus();
                    affectedByRaF = true;
                    break;
                case RestTypes.Honor:
                    // Reset restBonus (Honor only) for players with max honor level.
                    if (_player.IsMaxHonorLevel())
                        restBonus = 0;

                    next_level_xp = GetNextLevelHonorBonus();
                    break;
                default:
                    return;
            }

            float rest_bonus_max = GetRestBonusMax(next_level_xp);

            if (restBonus < 0)
                restBonus = 0;

            if (restBonus > rest_bonus_max)
                restBonus = rest_bonus_max;

            uint oldBonus = (uint)_restBonus[(int)restType];
            _restBonus[(int)restType] = restBonus;

            PlayerRestState oldRestState = (PlayerRestState)(int)_player.m_activePlayerData.RestInfo[(int)restType].StateID;
            PlayerRestState newRestState = PlayerRestState.Normal;

            if (affectedByRaF && _player.GetsRecruitAFriendBonus(true)
                && (_player.GetSession().IsARecruiter() || _player.GetSession().GetRecruiterId() != 0))
            {
                newRestState = PlayerRestState.RAFLinked;
            }
            else if (_restBonus[(int)restType] >= 1)
                newRestState = PlayerRestState.Rested;

            if (oldBonus == restBonus && oldRestState == newRestState)
                return;

            // update data for client
            _player.SetRestThreshold(restType, (int)_restBonus[(int)restType]);
            _player.SetRestState(restType, newRestState);

            // XpBonusDeposit needs to be reset after it taken into account 
            if (restType == RestTypes.XP)
                _XpBonusDeposit = 0;
        }

        public void AddRestBonus(RestTypes restType, float restBonus)
        {
            float totalRestBonus = GetRestBonus(restType) + restBonus;
            SetRestBonus(restType, totalRestBonus);
        }

        public void SetRestFlag(RestFlag restFlag, int triggerId = 0)
        {
            RestFlag oldRestMask = _restFlagMask;
            _restFlagMask |= restFlag;

            if (oldRestMask == 0 && _restFlagMask != 0) // only set flag/time on the first rest state
            {
                _restTime = LoopTime.ServerTime;
                _player.SetPlayerFlag(PlayerFlags.Resting);
            }

            if (triggerId != 0)
                _innAreaTriggerId = triggerId;
        }

        public void RemoveRestFlag(RestFlag restFlag)
        {
            RestFlag oldRestMask = _restFlagMask;
            _restFlagMask &= ~restFlag;

            if (oldRestMask != 0 && _restFlagMask == 0) // only remove flag/time on the last rest state remove
            {
                Update(true); // update freezed timer
                _restTime = ServerTime.Zero;
                _player.RemovePlayerFlag(PlayerFlags.Resting);
            }
        }

        public int GetRestBonusFor(RestTypes restType, int xp)
        {
            int rested_bonus = (int)GetRestBonus(restType); // xp for each rested bonus

            if (rested_bonus > xp) // max rested_bonus == xp or (r+x) = 200% xp
                rested_bonus = xp;

            int rested_loss = rested_bonus;
            if (restType == RestTypes.XP)
               MathFunctions.AddPct(ref rested_loss, _player.GetTotalAuraModifier(AuraType.ModRestedXpConsumption));

            SetRestBonus(restType, GetRestBonus(restType) - rested_loss);

            Log.outDebug(LogFilter.Player, 
                $"RestMgr.GetRestBonus: Player '{_player.GetGUID()}' ({_player.GetName()}) " +
                $"gain {xp + rested_bonus} xp (+{rested_bonus} Rested Bonus). " +
                $"Rested points={GetRestBonus(restType)}");

            return rested_bonus;
        }

        public void Update(bool skipTimer = false, bool skipClientUpdate = false)
        {
            if (_restTime == ServerTime.Zero)
                return;

            if (GetNextLevelXpBonus() == 0)
                return;

            if (LoopTime.ServerTime == _restTime)
                return;

            Seconds timeDiff = (Seconds)(LoopTime.ServerTime - _restTime);

            if (timeDiff > (Minutes)1 || skipTimer) // freeze update
            {
                float bubble = 0.125f * WorldConfig.Values[WorldCfg.RateRestIngame].Float;
                float bonus = timeDiff * CalcExtraPerSec(RestTypes.XP, bubble);

                _XpBonusDeposit += bonus;

                _restTime = LoopTime.ServerTime;

                if (skipClientUpdate)
                    return;

                if (_XpBonusDeposit >= GetNextLevelXpBonus() / 1000) // 0.1% from total bar
                {
                    SetRestBonus(RestTypes.XP, _restBonus[(int)RestTypes.XP] + _XpBonusDeposit);
                }
            }
        }

        public void LoadRestBonus(RestTypes restType, PlayerRestState state, float restBonus)
        {
            _restBonus[(int)restType] = restBonus;
            _player.SetRestState(restType, state);
            _player.SetRestThreshold(restType, (int)restBonus);
        }

        public float CalcExtraPerSec(RestTypes restType, float bubble)
        {
            switch (restType)
            {
                case RestTypes.Honor:
                    return _player.m_activePlayerData.HonorNextLevel / 72000.0f * bubble;
                case RestTypes.XP:
                    return _player.m_activePlayerData.NextLevelXP / 72000.0f * bubble;
                default:
                    return 0.0f;
            }
        }

        public float GetRestBonus(RestTypes restType)
        {
            float totalBonus = _restBonus[(int)restType];
            if (restType == RestTypes.XP)
            {
                Update(true, true);
                totalBonus += _XpBonusDeposit;
            }

            return totalBonus; 
        }

        public bool HasRestFlag(RestFlag restFlag) { return (_restFlagMask & restFlag) != 0; }
        public int GetInnTriggerId() { return _innAreaTriggerId; }
    }
}
