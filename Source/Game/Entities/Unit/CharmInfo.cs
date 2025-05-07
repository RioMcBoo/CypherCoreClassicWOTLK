// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Collections;
using Framework.Constants;
using Game.Networking;
using Game.Spells;
using System;
using System.Numerics;

namespace Game.Entities
{
    public class CharmInfo
    {
        public CharmInfo(Unit unit)
        {
            _unit = unit;
            _CommandState = CommandStates.Follow;
            _petnumber = 0;
            _oldReactState = ReactStates.Passive;
            for (byte i = 0; i < SharedConst.MaxSpellCharm; ++i)
            {
                _charmspells[i] = new();
            }

            for (var i = 0; i < SharedConst.ActionBarIndexMax; ++i)
                PetActionBar[i] = new();

            Creature creature = _unit.ToCreature();
            if (creature != null)
            {
                _oldReactState = creature.GetReactState();
                creature.SetReactState(ReactStates.Passive);
            }
        }

        public void RestoreState()
        {
            if (_unit.IsTypeId(TypeId.Unit))
            {
                Creature creature = _unit.ToCreature();
                if (creature != null)
                    creature.SetReactState(_oldReactState);
            }
        }

        public void InitPetActionBar()
        {
            // the first 3 SpellOrActions are attack, follow and stay
            for (byte i = 0; i < SharedConst.ActionBarIndexPetSpellStart - SharedConst.ActionBarIndexStart; ++i)
                SetActionBar((byte)(SharedConst.ActionBarIndexStart + i), (int)CommandStates.Attack - i, ActiveStates.Command);

            // middle 4 SpellOrActions are spells/special attacks/abilities
            for (byte i = 0; i < SharedConst.ActionBarIndexPetSpellEnd - SharedConst.ActionBarIndexPetSpellStart; ++i)
                SetActionBar((byte)(SharedConst.ActionBarIndexPetSpellStart + i), 0, ActiveStates.Passive);

            // last 3 SpellOrActions are reactions
            for (byte i = 0; i < SharedConst.ActionBarIndexEnd - SharedConst.ActionBarIndexPetSpellEnd; ++i)
                SetActionBar((byte)(SharedConst.ActionBarIndexPetSpellEnd + i), (int)CommandStates.Attack - i, ActiveStates.Reaction);
        }

        public void InitEmptyActionBar(bool withAttack = true)
        {
            if (withAttack)
                SetActionBar(SharedConst.ActionBarIndexStart, (int)CommandStates.Attack, ActiveStates.Command);
            else
                SetActionBar(SharedConst.ActionBarIndexStart, 0, ActiveStates.Passive);
            for (byte x = SharedConst.ActionBarIndexStart + 1; x < SharedConst.ActionBarIndexEnd; ++x)
                SetActionBar(x, 0, ActiveStates.Passive);
        }

        public void InitPossessCreateSpells()
        {
            if (_unit.IsTypeId(TypeId.Unit))
            {
                // Adding switch until better way is found. Malcrom
                // Adding entrys to this switch will prevent COMMAND_ATTACK being added to pet bar.
                switch (_unit.GetEntry())
                {
                    case 23575: // Mindless Abomination
                    case 24783: // Trained Rock Falcon
                    case 27664: // Crashin' Thrashin' Racer
                    case 40281: // Crashin' Thrashin' Racer
                    case 28511: // Eye of Acherus
                        break;
                    default:
                        InitEmptyActionBar();
                        break;
                }

                for (byte i = 0; i < SharedConst.MaxCreatureSpells; ++i)
                {
                    var spellId = _unit.ToCreature().m_spells[i];
                    SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellId, _unit.GetMap().GetDifficultyID());
                    if (spellInfo != null)
                    {
                        if (spellInfo.HasAttribute(SpellAttr5.NotAvailableWhileCharmed))
                            continue;

                        if (spellInfo.IsPassive())
                            _unit.CastSpell(_unit, spellInfo.Id, new CastSpellExtraArgs(true));
                        else
                            AddSpellToActionBar(spellInfo, ActiveStates.Passive, i % SharedConst.ActionBarIndexMax);
                    }
                }
            }
            else
                InitEmptyActionBar();
        }

        public void InitCharmCreateSpells()
        {
            if (_unit.IsTypeId(TypeId.Player))                // charmed players don't have spells
            {
                InitEmptyActionBar();
                return;
            }

            InitPetActionBar();

            for (int x = 0; x < SharedConst.MaxSpellCharm; ++x)
            {
                var spellId = _unit.ToCreature().m_spells[x];
                SpellInfo spellInfo = Global.SpellMgr.GetSpellInfo(spellId, _unit.GetMap().GetDifficultyID());

                if (spellInfo == null)
                {
                    _charmspells[x] = new(spellId, ActiveStates.Disabled);
                    continue;
                }

                if (spellInfo.HasAttribute(SpellAttr5.NotAvailableWhileCharmed))
                    continue;

                if (spellInfo.IsPassive())
                {
                    _unit.CastSpell(_unit, spellInfo.Id, new CastSpellExtraArgs(true));
                    _charmspells[x] = new(spellId, ActiveStates.Passive);
                }
                else
                {
                    _charmspells[x] = new(spellId, ActiveStates.Disabled);

                    ActiveStates newstate;

                    if (!spellInfo.IsAutocastable())
                        newstate = ActiveStates.Passive;
                    else
                    {
                        if (spellInfo.NeedsExplicitUnitTarget())
                        {
                            newstate = ActiveStates.Enabled;
                            ToggleCreatureAutocast(spellInfo, true);
                        }
                        else
                            newstate = ActiveStates.Disabled;
                    }

                    AddSpellToActionBar(spellInfo, newstate);
                }
            }
        }

        public bool AddSpellToActionBar(SpellInfo spellInfo, ActiveStates newstate = ActiveStates.Decide, int preferredSlot = 0)
        {
            var spell_id = spellInfo.Id;
            var first_id = spellInfo.GetFirstRankSpell().Id;

            // new spell rank can be already listed
            for (byte i = 0; i < SharedConst.ActionBarIndexMax; ++i)
            {
                var action = PetActionBar[i].Action;
                if (action != 0)
                {
                    if (PetActionBar[i].IsSpell && Global.SpellMgr.GetFirstSpellInChain(action) == first_id)
                    {
                        PetActionBar[i].Action = spell_id;
                        return true;
                    }
                }
            }

            // or use empty slot in other case
            for (byte i = 0; i < SharedConst.ActionBarIndexMax; ++i)
            {
                byte j = (byte)((preferredSlot + i) % SharedConst.ActionBarIndexMax);
                if (PetActionBar[j].Action == 0 && PetActionBar[j].IsSpell)
                {
                    SetActionBar(j, spell_id, newstate == ActiveStates.Decide ? spellInfo.IsAutocastable() ? ActiveStates.Disabled : ActiveStates.Passive : newstate);
                    return true;
                }
            }

            return false;
        }

        public bool RemoveSpellFromActionBar(int spell_id)
        {
            var first_id = Global.SpellMgr.GetFirstSpellInChain(spell_id);

            for (byte i = 0; i < SharedConst.ActionBarIndexMax; ++i)
            {
                var action = PetActionBar[i].Action;
                if (action != 0)
                {
                    if (PetActionBar[i].IsSpell && Global.SpellMgr.GetFirstSpellInChain(action) == first_id)
                    {
                        SetActionBar(i, 0, ActiveStates.Passive);
                        return true;
                    }
                }
            }

            return false;
        }

        public void ToggleCreatureAutocast(SpellInfo spellInfo, bool apply)
        {
            if (spellInfo.IsPassive())
                return;

            for (uint x = 0; x < SharedConst.MaxSpellCharm; ++x)
            {
                if (spellInfo.Id == _charmspells[x].Action)
                    _charmspells[x].State = apply ? ActiveStates.Enabled : ActiveStates.Disabled;
            }
        }

        public void SetPetNumber(int petnumber, bool statwindow)
        {
            _petnumber = petnumber;
            if (statwindow)
                _unit.SetPetNumberForClient(_petnumber);
            else
                _unit.SetPetNumberForClient(0);
        }

        public void LoadPetActionBar(string data)
        {
            InitPetActionBar();

            var tokens = new StringArray(data, ' ');
            if (tokens.Length != (SharedConst.ActionBarIndexEnd - SharedConst.ActionBarIndexStart) * 2)
                return;                                             // non critical, will reset to default

            byte index = 0;
            for (byte i = 0; i < tokens.Length && index < SharedConst.ActionBarIndexEnd; ++i, ++index)
            {
                ActiveStates type = tokens[i++].ToEnum<ActiveStates>();
                int.TryParse(tokens[i], out int action);

                PetActionBar[index] = new(action, type);

                // check correctness
                if (PetActionBar[index].IsSpell)
                {
                    SpellInfo spelInfo = Global.SpellMgr.GetSpellInfo(PetActionBar[index].Action, _unit.GetMap().GetDifficultyID());
                    if (spelInfo == null)
                        SetActionBar(index, 0, ActiveStates.Passive);
                    else if (!spelInfo.IsAutocastable())
                        SetActionBar(index, PetActionBar[index].Action, ActiveStates.Passive);
                }
            }
        }

        public void BuildActionBar(WorldPacket data)
        {
            for (int i = 0; i < SharedConst.ActionBarIndexMax; ++i)
                data.WriteUInt32(PetActionBar[i].PackedData);
        }

        public void SetSpellAutocast(SpellInfo spellInfo, bool state)
        {
            for (byte i = 0; i < SharedConst.ActionBarIndexMax; ++i)
            {
                if (spellInfo.Id == PetActionBar[i].Action && PetActionBar[i].IsSpell)
                {
                    PetActionBar[i].State = state ? ActiveStates.Enabled : ActiveStates.Disabled;
                    break;
                }
            }
        }

        public void SetIsCommandAttack(bool val)
        {
            _isCommandAttack = val;
        }

        public bool IsCommandAttack()
        {
            return _isCommandAttack;
        }

        public void SetIsCommandFollow(bool val)
        {
            _isCommandFollow = val;
        }

        public bool IsCommandFollow()
        {
            return _isCommandFollow;
        }

        public void SaveStayPosition()
        {
            //! At this point a new spline destination is enabled because of Unit.StopMoving()
            Vector3 stayPos = _unit.MoveSpline.FinalDestination();

            if (_unit.MoveSpline.onTransport)
            {
                float o = 0;
                ITransport transport = _unit.GetDirectTransport();
                if (transport != null)
                    transport.CalculatePassengerPosition(ref stayPos.X, ref stayPos.Y, ref stayPos.Z, ref o);
            }

            _stayX = stayPos.X;
            _stayY = stayPos.Y;
            _stayZ = stayPos.Z;
        }

        public void GetStayPosition(out float x, out float y, out float z)
        {
            x = _stayX;
            y = _stayY;
            z = _stayZ;
        }

        public void SetIsAtStay(bool val)
        {
            _isAtStay = val;
        }

        public bool IsAtStay()
        {
            return _isAtStay;
        }

        public void SetIsFollowing(bool val)
        {
            _isFollowing = val;
        }

        public bool IsFollowing()
        {
            return _isFollowing;
        }

        public void SetIsReturning(bool val)
        {
            _isReturning = val;
        }

        public bool IsReturning()
        {
            return _isReturning;
        }

        public int GetPetNumber() { return _petnumber; }
        public void SetCommandState(CommandStates st) { _CommandState = st; }
        public CommandStates GetCommandState() { return _CommandState; }
        public bool HasCommandState(CommandStates state) { return (_CommandState == state); }

        public void SetActionBar(byte index, int spellOrAction, ActiveStates state)
        {
            PetActionBar[index] = new(spellOrAction, state);
        }
        public CharmActionButton GetActionBarEntry(byte index) { return PetActionBar[index]; }

        public CharmActionButton GetCharmSpell(byte index) { return _charmspells[index]; }

        Unit _unit;
        CharmActionButton[] PetActionBar = new CharmActionButton[SharedConst.ActionBarIndexMax];
        CharmActionButton[] _charmspells = new CharmActionButton[4];
        CommandStates _CommandState;
        int _petnumber;

        ReactStates _oldReactState;

        bool _isCommandAttack;
        bool _isCommandFollow;
        bool _isAtStay;
        bool _isFollowing;
        bool _isReturning;
        float _stayX;
        float _stayY;
        float _stayZ;
    }

    public struct CharmActionButton
    {
        uint _packedData;

        public CharmActionButton()
        {
           State = ActiveStates.Disabled;
        }

        public CharmActionButton(uint packedData)
        {
            _packedData = packedData;
        }

        public CharmActionButton(int action, ActiveStates state)
        {
            _packedData = MAKE_UNIT_ACTION_STATE(action, state);
        }

        public uint PackedData => _packedData;

        public ActiveStates State
        {
            get => UNIT_ACTION_BUTTON_STATE(_packedData);
            set => _packedData = MAKE_UNIT_ACTION_STATE(Action, value);
        }

        public int Action
        {
            get => UNIT_ACTION_STATE_ACTION(_packedData);
            set => _packedData = MAKE_UNIT_ACTION_STATE(value, State);
        }

        public bool IsSpell
        {
            get
            {
                ActiveStates state = State;
                return state == ActiveStates.Disabled || state == ActiveStates.Enabled || state == ActiveStates.Passive;
            }
        }

        public bool IsCommand
        {
            get
            {
                ActiveStates state = State;
                return state == ActiveStates.Command || state == ActiveStates.Reaction;
            }
        }

        static uint MAKE_UNIT_ACTION_STATE(int action, ActiveStates state)
        {
            return (uint)(action | ((int)state << 23));
        }

        static int UNIT_ACTION_STATE_ACTION(uint packedData)
        {
            return (int)(packedData & 0x007FFFFF);
        }

        static ActiveStates UNIT_ACTION_BUTTON_STATE(uint packedData)
        {
            return (ActiveStates)((packedData & 0xFF800000) >>> 23);
        }
    }
}
