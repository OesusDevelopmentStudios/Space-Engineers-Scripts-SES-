using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {// UTILITY

        readonly string
            ROTATING_LIGHT,
            DARK_RED_BLINKING_LIGHT,
            DARK_RED_UNBLINKING_LIGHT,
            BLUE_BLINKING_LIGHT,
            BLUE_UNBLINKING_OFF_LIGHT;

        bool
            onAlert;

        bool IsOnThisGrid( IMyCubeBlock block ) { return Me.CubeGrid.Equals( block.CubeGrid ); }

        void SayMyName( string ScriptName, float textSize = 2f )
        {
            ScriptName = "\n\n" + ScriptName;
            IMyTextSurface surface = Me.GetSurface( 0 );
            surface.Alignment = TextAlignment.CENTER;
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.FontSize = textSize;
            surface.WriteText( ScriptName );
        }

        public Program( )
        {
            SayMyName( "RED ALERT" );
            onAlert = true;

            ROTATING_LIGHT              = "Rotating Light";
            DARK_RED_BLINKING_LIGHT     = "Dark Red Blinking Light";
            DARK_RED_UNBLINKING_LIGHT   = "Dark Red Unblinking Light";
            BLUE_BLINKING_LIGHT         = "Blue Blinking Light";
            BLUE_UNBLINKING_OFF_LIGHT   = "Blue Unblinking Off Light";

            FindAndPreparePotentialLights( );
            SwitchRedAlert( );
        }

        public bool StringEqualsOneOfLightTypes( string input )
        {
            if ( input.Equals( ROTATING_LIGHT ) || input.Equals( DARK_RED_BLINKING_LIGHT ) || input.Equals( DARK_RED_UNBLINKING_LIGHT ) || input.Equals( BLUE_BLINKING_LIGHT ) || input.Equals( BLUE_UNBLINKING_OFF_LIGHT ) ) return true;
            return false;
        }

        public bool StringContainsOneOfLightTypes( string input )
        {
            if ( input.Contains( ROTATING_LIGHT ) || input.Contains( DARK_RED_BLINKING_LIGHT ) || input.Contains( DARK_RED_UNBLINKING_LIGHT ) || input.Contains( BLUE_BLINKING_LIGHT ) || input.Contains( BLUE_UNBLINKING_OFF_LIGHT ) ) return true;
            return false;
        }


        public void SetWeapons( bool turn )
        {
            List<IMyLargeTurretBase> temp = new List<IMyLargeTurretBase>( );
            GridTerminalSystem.GetBlocksOfType( temp );

            foreach ( IMyLargeTurretBase tb in temp )
            {
                tb.Enabled = turn;
            }
        }

        public void CloseAllDoors( )
        {
            List<IMyDoor> temp = new List<IMyDoor>( );
            GridTerminalSystem.GetBlocksOfType( temp );

            foreach ( IMyDoor d in temp )
            {
                d.CloseDoor( );
            }
        }

        public List<IMyLightingBlock> GetLights( string name )
        {
            List<IMyLightingBlock>
                result = new List<IMyLightingBlock>( ),
                temp = new List<IMyLightingBlock>( );

            GridTerminalSystem.GetBlocksOfType( temp );

            foreach ( IMyLightingBlock light in temp )
            {
                if ( IsOnThisGrid( light ) )
                {
                    if ( light.CustomData.Equals( name ) )
                        result.Add( light );
                    else
                    {
                        if ( light.CustomName.Contains( name ) )
                        {
                            light.CustomData = name;
                            result.Add( light );
                        }
                    }
                }
            }

            return result;
        }

        public void FindAndPreparePotentialLights( )
        {
            List<IMyLightingBlock> temp = new List<IMyLightingBlock>( );
            GridTerminalSystem.GetBlocksOfType( temp );
            foreach ( IMyLightingBlock bl in temp )
            {
                if (
                    IsOnThisGrid( bl ) &&
                    !ShouldBeIgnored( bl ) &&
                    !StringEqualsOneOfLightTypes( bl.CustomData ) &&
                    !StringContainsOneOfLightTypes( bl.CustomName )
                )
                {
                    if ( bl.BlockDefinition.SubtypeName.Equals( "RotatingLightLarge" )
                       || bl.BlockDefinition.SubtypeName.Equals( "RotatingLightSmall" ) )
                    {
                        bl.CustomData = ROTATING_LIGHT;
                    }
                    else
                    if ( bl.BlockDefinition.SubtypeName.Equals( "LargeBlockFrontLight" )
                       || bl.BlockDefinition.SubtypeName.Equals( "SmallBlockFrontLight" ) )
                    {
                        bl.CustomData = "Spotlight";
                    }
                    else
                    {
                        bl.CustomData = DARK_RED_UNBLINKING_LIGHT;
                    }
                }
            }
        }

        public void ChangeLightsStatus( List<IMyLightingBlock> target, bool turnon )
        {
            foreach ( IMyLightingBlock LB in target )
            {
                LB.Enabled = turnon;
            }
        }

        public void ChangeLightsColor( List<IMyLightingBlock> target, Color color )
        {
            foreach ( IMyLightingBlock LB in target )
            {
                LB.Color = color;
            }
        }

        public void SwitchRedAlert( )
        {

            List<IMyLightingBlock>
                rotatingLights = GetLights( ROTATING_LIGHT ),
                blueUnblinkingOffLights = GetLights( BLUE_UNBLINKING_OFF_LIGHT ),
                blueBlinkingLights = GetLights( BLUE_BLINKING_LIGHT ),
                darkRedBlinkingLights = GetLights( DARK_RED_BLINKING_LIGHT ),
                darkRedUnblinkingLights = GetLights( DARK_RED_UNBLINKING_LIGHT );

            onAlert = !onAlert;

            if ( onAlert )
            {   // turn the <s> Fucking Furries </s> on
                ChangeLightsColor( darkRedBlinkingLights, new Color( 60, 0, 0 ) );
                ChangeLightsColor( blueBlinkingLights, new Color( 0, 0, 255 ) );
                ChangeLightsColor( blueUnblinkingOffLights, new Color( 0, 0, 255 ) );
                ChangeLightsColor( darkRedUnblinkingLights, new Color( 60, 0, 0 ) );
                ChangeLightsColor( rotatingLights, new Color( 255, 0, 0 ) );
                ChangeLightsStatus( rotatingLights, true );
                ChangeLightsStatus( blueUnblinkingOffLights, true );
                foreach ( IMyLightingBlock l in darkRedBlinkingLights )
                {
                    l.BlinkLength = 50f;
                    l.BlinkOffset = 50f;
                    l.BlinkIntervalSeconds = 2;
                }
                foreach ( IMyLightingBlock l in blueBlinkingLights )
                {
                    l.BlinkLength = 50f;
                    l.BlinkIntervalSeconds = 2;
                }
                SetWeapons( true );
                CloseAllDoors( );
            }
            else
            {   // turn the <s> Fucking Furries </s> off
                ChangeLightsColor( darkRedBlinkingLights, new Color( 255, 255, 255 ) );
                ChangeLightsColor( blueBlinkingLights, new Color( 255, 255, 255 ) );
                ChangeLightsColor( darkRedUnblinkingLights, new Color( 255, 255, 255 ) );
                ChangeLightsStatus( rotatingLights, false );
                ChangeLightsStatus( blueUnblinkingOffLights, false );
                foreach ( IMyLightingBlock l in darkRedBlinkingLights )
                {
                    l.BlinkIntervalSeconds = 0;
                }
                foreach ( IMyLightingBlock l in blueBlinkingLights )
                {
                    l.BlinkIntervalSeconds = 0;
                }
            }
        }

        public bool ShouldBeIgnored( IMyTerminalBlock block )
        {
            string
                name = block.CustomName.ToLower( ),
                data = block.CustomData.ToLower( );

            if ( name.Contains( "ignore" ) || data.Contains( "ignore" ) ) return true;
            return false;
        }

        public string UnpackList( List<object> list )
        {
            string output = "";
            foreach ( object obj in list )
            {
                output += obj.ToString( ) + "\n";
            }
            return output;
        }

        public string UnpackList( List<ITerminalAction> list )
        {
            string output = "";
            foreach ( ITerminalAction obj in list )
            {
                output += obj.Name + "\n";
            }
            return output;
        }

        public void Main( string argument )
        {
            String[ ] eval = argument.Split( ' ' );

            if ( eval.Length <= 0 )
                SwitchRedAlert( );
            else
                switch ( eval[0].ToLower( ) )
                {
                    case "lazy":
                        FindAndPreparePotentialLights( );
                        break;

                    case "test":
                        string chckName = argument.Substring( eval[0].Length + 1 );
                        IMyTerminalBlock block = GridTerminalSystem.GetBlockWithName( chckName );
                        if ( block == null )
                        {
                            Echo( "UnU" );
                            return;
                        }
                        Echo( block.GetType( ).FullName );
                        List<ITerminalAction> list = new List<ITerminalAction>( );
                        block.GetActions( list );
                        Echo( block.BlockDefinition.SubtypeId );
                        Echo( UnpackList( list ) );
                        break;


                    default: SwitchRedAlert( ); break;
                }
        }
    }
}
