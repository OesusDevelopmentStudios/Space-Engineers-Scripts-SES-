using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {

        public const string PROGRAM_TAG = "airlck";
        public const int 
            ITERATIONS_PER_DOOR_TICK = 4,
            ITERATIONS_WAITING_FOR_USER = 3;
        private readonly Dictionary<string, Airlock> airlocks = new Dictionary<string, Airlock>( );

        public class Job
        {
            private readonly List<IMyDoor> doors;
            private readonly JobType type;
            public int ticksToJob;

            public Job( List<IMyDoor> door, JobType type, int TTJ )
            {
                this.doors = door;
                this.type = type;
                this.ticksToJob = TTJ;
            }

            public void Enable(bool yes)
            {
                foreach ( IMyDoor door in doors )
                    door.Enabled = yes;
            }

            public void Open(bool yes )
            {
                if ( yes )
                    foreach ( IMyDoor door in doors )
                        door.OpenDoor( );
                else
                    foreach ( IMyDoor door in doors )
                        door.CloseDoor( );
            }

            public void Perform( )
            {
                Enable(type != JobType.DISABLE);
                switch ( type )
                {
                    case JobType.OPEN:
                        Open( true );
                        break;

                    case JobType.CLOSE:
                        Open( false );
                        break;
                }
            }
        }

        public enum JobType
        {
            OPEN,
            CLOSE,
            DISABLE
        }

        public class Airlock
        {
            private readonly List<IMyDoor> A = new List<IMyDoor>(), B = new List<IMyDoor>( );
            private AirlockOperationMode mode = AirlockOperationMode.TIME_CONTROLLED_FULL_CYCLE;
            private FinalAirlockState finalState = FinalAirlockState.CLOSE_BOTH;
            private List<Job> jobs = new List<Job>();

            public void AddA(IMyDoor A)
            {
                this.A.Add(A);
            }
            
            public void AddB( IMyDoor B )
            {
                this.B.Add( B);
            }

            public void SetMode( AirlockOperationMode mode )
            {
                this.mode = mode;
            }

            public void SetFinalState(FinalAirlockState finalState )
            {
                this.finalState = finalState;
            }

            public int GetToggleTimeForDoorType( IMyDoor door )
            {
                if ( door.BlockDefinition.SubtypeName.Contains( "SlidingHatchDoor" ) ) return 3;
                //if ( door.BlockDefinition.SubtypeName.Equals( "" ) || door.BlockDefinition.SubtypeName.Equals( "SmallDoor" ) ) return 1;
                return 1;
            }

            public DoorStatus GetStatusOfDoors( bool isDoorA )
            {
                Dictionary<DoorStatus, int> statuses = new Dictionary<DoorStatus, int>( );
                int maxVal;
                DoorStatus maxKey;
                statuses.Add( DoorStatus.Closed, 0 );
                statuses.Add( DoorStatus.Closing, 0 );
                statuses.Add( DoorStatus.Open, 0 );
                statuses.Add( DoorStatus.Opening, 0 );

                List<IMyDoor> doors = isDoorA ? A : B;
                foreach ( IMyDoor door in doors )
                    statuses[door.Status]++;

                maxVal = statuses[DoorStatus.Opening]; maxKey = DoorStatus.Opening;
                foreach(DoorStatus stat in statuses.Keys )
                {
                    if(statuses[stat] > maxVal )
                    {
                        maxVal = statuses[stat];
                        maxKey = stat;
                    }
                }

                return maxKey;
            }

            public bool GetEnabledOfDoors(bool isDoorA )
            {
                int
                    enabledNo = 0,
                    disabledNo = 0;

                foreach ( IMyDoor door in isDoorA ? A : B )
                    if ( door.Enabled )
                        enabledNo++;
                    else
                        disabledNo++;

                return enabledNo >= disabledNo;
            }

            public void Enable( bool isDoorA, bool yes )
            {
                foreach ( IMyDoor door in isDoorA? A:B )
                    door.Enabled = yes;
            }

            public void OpenSide( bool isDoorA )
            {
                if ( jobs.Count > 0 || A.Count <= 0 || B.Count <= 0 ) return;


                List < IMyDoor >
                    primary = isDoorA ? A : B,
                    secondary = isDoorA ? B : A;

                /// secondary

                int TTJ = 0;
                if ( !GetStatusOfDoors( !isDoorA ).Equals( DoorStatus.Closed ) )
                {
                    AddJob( new Job( secondary, JobType.CLOSE, TTJ ) ); TTJ += ITERATIONS_PER_DOOR_TICK * GetToggleTimeForDoorType( secondary[0] );
                    AddJob( new Job( secondary, JobType.DISABLE, TTJ ) );
                }
                else if ( GetEnabledOfDoors( !isDoorA ) ) Enable( isDoorA, false );
                AddJob( new Job( primary, JobType.OPEN, TTJ ) );

                if ( !mode.Equals( AirlockOperationMode.HARDWARE_CONTROLLED ) )
                {
                    TTJ += ITERATIONS_PER_DOOR_TICK * ( ITERATIONS_WAITING_FOR_USER + GetToggleTimeForDoorType( primary[0] ) );
                    AddJob( new Job( primary, JobType.CLOSE, TTJ ) ); TTJ += ITERATIONS_PER_DOOR_TICK * GetToggleTimeForDoorType( primary[0] );
                    AddJob( new Job( primary, JobType.DISABLE, TTJ ) );
                    if ( mode.Equals( AirlockOperationMode.TIME_CONTROLLED_NO_CYCLE ) ) return;
                    AddJob( new Job( secondary, JobType.OPEN, TTJ ) );

                    if ( !finalState.Equals( FinalAirlockState.LEAVE_AS_IS ) )
                    {
                        TTJ += ITERATIONS_PER_DOOR_TICK * ( GetToggleTimeForDoorType( secondary[0] ) + ITERATIONS_WAITING_FOR_USER );
                        if ( finalState.Equals( FinalAirlockState.CLOSE_BOTH ) )
                        {
                            AddJob( new Job( secondary, JobType.CLOSE, TTJ ) ); TTJ += ITERATIONS_PER_DOOR_TICK * GetToggleTimeForDoorType( secondary[0] );
                            AddJob( new Job( secondary, JobType.DISABLE, TTJ ) );
                        }
                        else if ( finalState.Equals( FinalAirlockState.CLOSE_A_SIDE ) && !isDoorA )
                        {
                            AddJob( new Job( A, JobType.CLOSE, TTJ ) ); TTJ += ITERATIONS_PER_DOOR_TICK * GetToggleTimeForDoorType( A[0] );
                            AddJob( new Job( A, JobType.DISABLE, TTJ ) );
                            AddJob( new Job( B, JobType.OPEN, TTJ ) );
                        }
                        else if ( finalState.Equals( FinalAirlockState.CLOSE_B_SIDE ) && isDoorA )
                        {
                            AddJob( new Job( B, JobType.CLOSE, TTJ ) ); TTJ += ITERATIONS_PER_DOOR_TICK * GetToggleTimeForDoorType( B[0] );
                            AddJob( new Job( B, JobType.DISABLE, TTJ ) );
                            AddJob( new Job( A, JobType.OPEN, TTJ ) );
                        }
                    }
                }
            }
            public void Toggle( )
            {
                if ( jobs.Count > 0 || finalState.Equals( FinalAirlockState.CLOSE_BOTH ) ) return;
                if ( GetStatusOfDoors(true).Equals( DoorStatus.Closed ) )
                {
                    OpenSide( true );
                }
                else
                if ( GetStatusOfDoors( false ).Equals( DoorStatus.Closed ) )
                {
                    OpenSide( false );
                }
            }

            public void DoYourJob( )
            {
                if ( jobs.Count <= 0 ) return;

                List<Job> temp = new List<Job>( );
                foreach ( Job job in jobs )
                {
                    if ( --job.ticksToJob <= 0 )
                    {
                        job.Perform( );
                    }
                    else
                    {
                        temp.Add( job );
                    }
                }
                jobs = temp;
            }

            public void AddJob( Job job )
            {
                jobs.Add( job );
            }
        }

        public enum FinalAirlockState
        {
            LEAVE_AS_IS,
            CLOSE_A_SIDE,
            CLOSE_B_SIDE,
            CLOSE_BOTH
        }

        public FinalAirlockState ParseState( string input )
        {
            switch ( input )
            {
                case "both":
                    return FinalAirlockState.CLOSE_BOTH;

                case "closeb":
                case "b":
                    return FinalAirlockState.CLOSE_B_SIDE;

                case "closea":
                case "a":
                    return FinalAirlockState.CLOSE_A_SIDE;

                default:
                    return FinalAirlockState.LEAVE_AS_IS;
            }
        }

        public enum AirlockOperationMode
        {
            TIME_CONTROLLED_NO_CYCLE,
            TIME_CONTROLLED_FULL_CYCLE,
            HARDWARE_CONTROLLED
        }

        public AirlockOperationMode ParseMode( string input )
        {
            switch ( input )
            {
                case "time":
                    return AirlockOperationMode.TIME_CONTROLLED_NO_CYCLE;

                case "cycle":
                case "fullcycle":
                    return AirlockOperationMode.TIME_CONTROLLED_FULL_CYCLE;

                default:
                    return AirlockOperationMode.HARDWARE_CONTROLLED;
            }
        }

        public bool IsOnThisGrid( IMyCubeBlock block )
        {
            return Me.CubeGrid.Equals( block.CubeGrid );
        }

        public void FindAllAirlocks()
        {
            List<IMyDoor> doors = new List<IMyDoor>( );
            GridTerminalSystem.GetBlocksOfType( doors );
            foreach(IMyDoor door in doors )
            {
                if(IsOnThisGrid(door) && door.CustomData.Length > 0 )
                {
                    string [ ] args = door.CustomData.ToLower().Split( ';' );
                    //if(args.Length) AIRLCK;PPAL45;A;TIME;BOTH
                    if ( args.Length > 2 && args[0].Equals( PROGRAM_TAG ) )
                    {
                        string airKey = args [1];
                        bool isDoorA = args [2].Equals( "a" );
                        AirlockOperationMode mode = args.Length>3? ParseMode(args[3]):AirlockOperationMode.TIME_CONTROLLED_FULL_CYCLE;
                        FinalAirlockState state = args.Length>4? ParseState(args[4]):FinalAirlockState.CLOSE_BOTH;

                        Airlock air;
                        if ( airlocks.TryGetValue( airKey, out air ) )
                            airlocks.Remove( airKey );
                        else
                            air = new Airlock( );

                        if ( isDoorA )
                            air.AddA( door );
                        else
                            air.AddB( door );

                        if ( !mode.Equals( AirlockOperationMode.TIME_CONTROLLED_FULL_CYCLE ) ) air.SetMode( mode );
                        if ( !state.Equals( FinalAirlockState.CLOSE_BOTH ) ) air.SetFinalState( state );

                        door.CustomName = String.Format("Door {0} - Airlock {1}", isDoorA? "A":"B", airKey.ToUpper());

                        airlocks.Add( airKey, air );
                    }
                }
            }
        }

        string GetFullScriptName( string ScriptName ) { return "[" + ScriptName + "] Script"; }
        void SayMyName( string ScriptName, float textSize = 2f )
        {
            Me.CustomName = GetFullScriptName( ScriptName );
            ScriptName = "\n\n" + ScriptName;
            IMyTextSurface surface = Me.GetSurface( 0 );
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText( ScriptName );
        }

        public Program( )
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            SayMyName( "AIRLOCK CONTROL" );
            FindAllAirlocks( );
        }

        public void Main( string argument, UpdateType updateSource )
        {

            if ( ( updateSource & ( UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100 ) ) > 0 )
            {
                foreach ( Airlock air in airlocks.Values )
                    air.DoYourJob( );
            }
            else
            {
                if ( argument.Length > 0 )
                {
                    string [ ] args = argument.ToLower( ).Split( ' ' );
                    string key;
                    bool isDoorA;
                    Airlock air;

                    switch ( args [0] )
                    {
                        case "open":
                            if ( args.Length > 2 )
                            {
                                key = args [1].ToLower();
                                isDoorA = args [2].Equals( "a" );
                                if ( airlocks.TryGetValue( key, out air ) )
                                {
                                    air.OpenSide( isDoorA );
                                }
                                //else throw new Exception( "lol byczku" );
                            }
                            break;

                        case "toggle":
                            if ( args.Length > 1 )
                            {
                                key = args [1].ToLower( );
                                if ( airlocks.TryGetValue( key, out air ) )
                                {
                                    air.Toggle( );
                                }
                                //else throw new Exception("lol byczku");
                            }
                            break;
                    }
                }
                else
                {
                    FindAllAirlocks( );
                }
            }
        }

    }
}
