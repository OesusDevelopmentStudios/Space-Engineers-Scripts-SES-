///	Settings

const string MainSensorName   = "Missile/Main Sensor";
const string LeftSensorName   = "Missile/Left Sensor";
const string RightSensorName  = "Missile/Right Sensor";
const string TopSensorName    = "Missile/Top Sensor";
const string BottomSensorName = "Missile/Bottom Sensor";

///	End of settings

/// Global variables

IMySensorBlock MainSensor   ;
IMySensorBlock LeftSensor   ;
IMySensorBlock RightSensor  ;
IMySensorBlock TopSensor    ;
IMySensorBlock BottomSensor ;

IMyShipController MISSILE_CONTROLLER;

double  STARTING_SPEED;
double  MAX_SPEED;

float   MIN_SENSOR_EXTEND =  2F;
float   MAX_SENSOR_EXTEND = 20F;

float   STARTING_DISTANCE;

bool    WasJustBraking = false;
bool    FoundDistance = false;

string CurrentMode = "INIT";

/// <summary>Those lists are automatically filled in the <c>FindThrusters</c> method </summary>
List<IMyThrust> MISSILE_FORWARD  = new List<IMyThrust>(); /// A List of Forward  thrusting thrusters. 
List<IMyThrust> MISSILE_BACKWARD = new List<IMyThrust>(); /// A List of Backward thrusting thrusters. 
List<IMyThrust> MISSILE_LEFT     = new List<IMyThrust>(); /// A List of Left     thrusting thrusters. 
List<IMyThrust> MISSILE_RIGHT    = new List<IMyThrust>(); /// A List of Right    thrusting thrusters. 
List<IMyThrust> MISSILE_UP       = new List<IMyThrust>(); /// A List of Up       thrusting thrusters. 
List<IMyThrust> MISSILE_DOWN     = new List<IMyThrust>(); /// A List of Down     thrusting thrusters. 

/// <summary> Method <c>ClearMissileThrusters</c> clears all of the Missile's Thrusters' Lists.</summary>
public void ClearMissileThrusters(){

    MISSILE_FORWARD .Clear();
    MISSILE_BACKWARD.Clear();
    MISSILE_LEFT    .Clear();
    MISSILE_RIGHT   .Clear();
    MISSILE_UP      .Clear();
    MISSILE_DOWN    .Clear();

}


/// End of global variables

/// UTIL FUNCTIONS

/// <summary> Method <c>Program</c> makes running the script 10^X times a second possible.</summary>
Program(){

    Runtime.UpdateFrequency = UpdateFrequency.Update1;

}

/// <summary> Method <c>InitMissile</c> attempts to initialize all of the Missile's subsystems.</summary>
public void InitMissile(){

    Echo("Initializing Sensors...");
    SetSensor(MainSensorName  );   MainSensor   = GridTerminalSystem.GetBlockWithName("Missile/Main Sensor"  ) as IMySensorBlock;
    SetSensor(LeftSensorName  );   LeftSensor   = GridTerminalSystem.GetBlockWithName("Missile/Left Sensor"  ) as IMySensorBlock;
    SetSensor(RightSensorName );   RightSensor  = GridTerminalSystem.GetBlockWithName("Missile/Right Sensor" ) as IMySensorBlock;
    SetSensor(TopSensorName   );   TopSensor    = GridTerminalSystem.GetBlockWithName("Missile/Top Sensor"   ) as IMySensorBlock;
    SetSensor(BottomSensorName);   BottomSensor = GridTerminalSystem.GetBlockWithName("Missile/Bottom Sensor") as IMySensorBlock;

    FindThrusters();

}

/// <summary> 
/// Method <c>SetSensor</c> sets the Sensor's parameters.
/// The one accepting only the <param>Name<param> parameter sets all of the sensor's
/// parameters to the default ones.
/// </summary>
public void SetSensor(string Name){

    IMySensorBlock target = GridTerminalSystem.GetBlockWithName(Name) as IMySensorBlock;
    target.LeftExtend   = 1F;
    target.RightExtend  = 1F;
    target.TopExtend    = 1F;
    target.BottomExtend = 1F;
    target.FrontExtend  = 1F;
    target.BackExtend   = 1F;

    target.DetectOwner    = false;
    target.DetectFriendly = false;
    target.DetectEnemy    = false;
    target.DetectNeutral  = false;

    target.DetectPlayers	     = false;
    target.DetectFloatingObjects = false;
    target.DetectSmallShips	     = true ;
    target.DetectLargeShips	     = true ;
    target.DetectStations		 = true ;
    target.DetectSubgrids		 = false;
    target.DetectAsteroids		 = false;
    target.PlayProximitySound    = false;
}

/// <summary> 
/// Method <c>SetSensor</c> sets the Sensor's parameters.
/// The one accepting the eight parameters sets them up based on the programmer's input.
/// </summary>
public void SetSensor(string Name, float Left, float Right, float Top, float Bottom, float Front, float Back, bool targetEnemies){

    IMySensorBlock target = GridTerminalSystem.GetBlockWithName(Name) as IMySensorBlock;
    target.LeftExtend   = Left  ;
    target.RightExtend  = Right ;
    target.TopExtend    = Top   ;
    target.BottomExtend = Bottom;
    target.FrontExtend  = Front ;
    target.BackExtend   = Back  ;

    if(targetEnemies){
        target.DetectOwner      = false;
        target.DetectFriendly	= false;
        target.DetectEnemy      = true;
    }
    else{
        target.DetectOwner      = true;
        target.DetectFriendly	= true;
        target.DetectEnemy      = false;
    }

}

/// <summary> 
/// Method <c>TranslateOrientation</c> attempts to turn an in-game <param>MyBlockOrientation</param>
/// and return it to a
/// <returns>
/// Double digits int, with each digit responding to a direction
///   1 - FORWARD
///   2 - BACKWARD
///   3 - LEFT
///   4 - RIGHT
///   5 - TOP
///   6 - BOTTOM
/// </returns>
/// for the programmer's sake (we don't want to get absolutely mad).
/// </summary> 
public int TranslateOrientation(MyBlockOrientation o){

    int translatedFW = TranslateDirection(o.Forward);
    int translatedUP = TranslateDirection(o.Up);
    if(translatedFW == 44 || translatedUP==44){Echo("*ANGERY SIREN NOISES*"); return 444;}
    else return translatedFW*10+translatedUP;

}

/// <summary> 
/// Method <c>TranslateDirection</c> attempts to return an in-game <param>VRageMath.Base6Directions.Direction</param>
/// and returns 
/// <returns>
/// An int number between 1 and 6
///   1 - FORWARD
///   2 - BACKWARD
///   3 - LEFT
///   4 - RIGHT
///   5 - TOP
///   6 - BOTTOM
/// </returns>
/// to make it easier.
/// </summary> 
public int TranslateDirection(VRageMath.Base6Directions.Direction d){

    switch(d){

        case VRageMath.Base6Directions.Direction.Forward: return 1;
        case VRageMath.Base6Directions.Direction.Backward:return 2;
        case VRageMath.Base6Directions.Direction.Left:    return 3;
        case VRageMath.Base6Directions.Direction.Right:   return 4;
        case VRageMath.Base6Directions.Direction.Up:      return 5;
        case VRageMath.Base6Directions.Direction.Down:    return 6;
        default: Echo("*ANGERY SIREN NOISES*"); return 44;

    }

}

/// <summary> 
/// Method <c>TranslateDirection</c> attempts to turn an in-game <param>IMyCubeBlock</param> Object's Direction
/// and return it to a
/// <returns>
/// Double digits int, with each digit responding to a direction
///   1 - FORWARD
///   2 - BACKWARD
///   3 - LEFT
///   4 - RIGHT
///   5 - TOP
///   6 - BOTTOM
/// </returns>
/// which is judged by the programming block's PoV
/// </summary> 
public int TranslateDirection(IMyCubeBlock block){

    int TSL = TranslateOrientation(Me.Orientation);
    int TFW = (TSL/10);
    int TUP = TSL - TFW*10;
    if(block is IMyThrust){

        int blockDir = TranslateDirection(block.Orientation.Forward);
        if(blockDir==TFW) return 2;
        if(blockDir==TUP) return 6;
        if(TFW%2==0){

        if(blockDir==TFW-1) return 1;
        else if(TUP%2==0){

            if(blockDir==TUP-1) return 5;
            else{

                if(blockDir%2==0) return 3;
                else return 4;

            }

        }
        else{

            if(blockDir==TUP+1) return 5;
            else{

                if(blockDir%2==0) return 3;
                else return 4;

            }
        }
        }
        else{
            if(blockDir==TFW+1) return 1;
            else if(TUP%2==0){

                if(blockDir==TUP-1) return 5;
                else{

                    if(blockDir%2==0) return 4;
                    else return 3;

                }
            }
            else{

                if(blockDir==TUP+1) return 5;
                else{

                    if(blockDir%2==0) return 4;
                    else return 3;

                }
            }
        }

    }
    else
    if(block is IMyGyro){

        int blockDir = TranslateDirection(block.Orientation.Forward);
        int blockSub = TranslateDirection(block.Orientation.Up);
        int firstDigit = 0;

        if(blockSub==TFW) firstDigit = 2;
        else if(blockSub==TUP) firstDigit = 6;
        else if(TFW%2==0){

        if(blockSub==TFW-1) firstDigit = 1;
        else if(TUP%2==0){

            if(blockSub==TUP-1) firstDigit = 5;
            else{

                if(blockSub%2==0) firstDigit = 3;
                else firstDigit = 4;

            }

        }
        else{

            if(blockSub==TUP+1) firstDigit = 5;
            else{

                if(blockSub%2==0) firstDigit = 3;
                else firstDigit = 4;

            }
        }
        }
        else{
            if(blockSub==TFW+1) firstDigit = 1;
            else if(TUP%2==0){

                if(blockSub==TUP-1) firstDigit = 5;
                else{

                    if(blockSub%2==0) firstDigit = 4;
                    else firstDigit = 3;

                }
            }
            else{

                if(blockSub==TUP+1) firstDigit = 5;
                else{

                    if(blockSub%2==0) firstDigit = 4;
                    else firstDigit = 3;

                }
            }
        }

        if(blockDir==TFW) return firstDigit*10+2;
        else if(blockDir==TUP) return firstDigit*10+6;
        else if(TFW%2==0){

        if(blockDir==TFW-1) return firstDigit*10+1;
        else if(TUP%2==0){

            if(blockDir==TUP-1) return firstDigit*10+5;
            else{

                if(blockDir%2==0) return firstDigit*10+3;
                else return firstDigit*10+4;

            }

        }
        else{

            if(blockDir==TUP+1) return firstDigit*10+5;
            else{

                if(blockDir%2==0) return firstDigit*10+3;
                else return firstDigit*10+4;

            }
        }
        }
        else{
            if(blockDir==TFW+1) return firstDigit*10+1;
            else if(TUP%2==0){

                if(blockDir==TUP-1) return firstDigit*10+5;
                else{

                    if(blockDir%2==0) return firstDigit*10+4;
                    else return firstDigit*10+3;

                }
            }
            else{

                if(blockDir==TUP+1) return firstDigit*10+5;
                else{

                    if(blockDir%2==0) return firstDigit*10+4;
                    else return firstDigit*10+3;

                }
            }
        }


    }
    else return 0;
}

/// <summary> 
/// Method <c>DirintToName</c> returns a
/// <returns>string that describes the Thruster's thrusting direction</returns>
/// from <param>dirint</param>
/// </summary>
public string DirintToName(int dirint){

    switch(dirint){

        case 1: return "FORWARD"; 
        case 2: return "BACKWARD";
        case 3: return "LEFT";    
        case 4: return "RIGHT";   
        case 5: return "UP";      
        case 6: return "DOWN";    
        default: return "ERROR";  

    }

}

/// <summary> 
/// Method <c>ChangeForwardExtend</c> changes a given <param>sensor</param>'s Forward extent by given <param>ammount</param>
/// </summary>
public void ChangeForwardExtend(IMySensorBlock sensor, float ammount){
    sensor.FrontExtend   =                          sensor.FrontExtend + ammount; 
    if(sensor.FrontExtend<MIN_SENSOR_EXTEND)        sensor.FrontExtend = MIN_SENSOR_EXTEND;
    else if(sensor.FrontExtend>MAX_SENSOR_EXTEND)   sensor.FrontExtend = MAX_SENSOR_EXTEND;
}

/// <summary> 
/// Method <c>ChangeSensorExtend</c> changes a given <param>sensor</param>'s main extent 
/// (forward for forward, left for left and so on) by given <param>ammount</param>
/// </summary>
public void ChangeSensorExtend(IMySensorBlock sensor, float ammount){

    switch(sensor.CustomName){

        case MainSensorName  : 
        sensor.FrontExtend   = sensor.FrontExtend + ammount; 
        if(sensor.FrontExtend<MIN_SENSOR_EXTEND) sensor.FrontExtend  = MIN_SENSOR_EXTEND;
        else if(sensor.FrontExtend>MAX_SENSOR_EXTEND) sensor.FrontExtend  = MAX_SENSOR_EXTEND;
        break;

        case LeftSensorName  : 
        sensor.LeftExtend    = sensor.LeftExtend + ammount; 
        if(sensor.LeftExtend<MIN_SENSOR_EXTEND) sensor.LeftExtend   = MIN_SENSOR_EXTEND; 
        else if(sensor.LeftExtend>MAX_SENSOR_EXTEND) sensor.LeftExtend   = MAX_SENSOR_EXTEND;
        break;

        case RightSensorName : 
        sensor.RightExtend = sensor.RightExtend  + ammount; 
        if(sensor.RightExtend<MIN_SENSOR_EXTEND) sensor.RightExtend  = MIN_SENSOR_EXTEND; 
        else if(sensor.RightExtend>MAX_SENSOR_EXTEND) sensor.RightExtend  = MAX_SENSOR_EXTEND;
        break;

        case TopSensorName   : 
        sensor.TopExtend = sensor.TopExtend    + ammount; 
        if(sensor.TopExtend<MIN_SENSOR_EXTEND) sensor.TopExtend    = MIN_SENSOR_EXTEND; 
        else if(sensor.TopExtend>MAX_SENSOR_EXTEND) sensor.TopExtend    = MAX_SENSOR_EXTEND;
        break;

        case BottomSensorName: 
        sensor.BottomExtend =sensor.BottomExtend  + ammount; 
        if(sensor.BottomExtend<MIN_SENSOR_EXTEND) sensor.BottomExtend = MIN_SENSOR_EXTEND; 
        else if(sensor.BottomExtend>MAX_SENSOR_EXTEND) sensor.BottomExtend = MAX_SENSOR_EXTEND;
        break;

        default: 
            Echo("DING DING DING DING DING DING DING DING.");
        break;

    }

}

/// <summary> 
/// Method <c>MoveAGroupThrusters</c> changes a given IMyThrust <param>Group</param>'s thrust override
/// to a given <param>OverridePercent</param>
/// </summary>
public void MoveAGroupThrusters(List<IMyThrust> Group, float OverridePercent){
    foreach(IMyThrust Thruster in Group){
        Thruster.ThrustOverridePercentage = OverridePercent;
    }
}

/// <summary> 
/// Method <c>EnableAGroupThrusters</c> sets given IMyThrust <param>Group</param>'s Enable value to given <param>Enable</param>.
/// </summary>
public void EnableAGroupThrusters(List<IMyThrust> Group, bool Enable){
    foreach(IMyThrust Thruster in Group){
        Thruster.Enabled = Enable;
    }
}

/// <summary> 
/// Method <c>MoveAllGyros</c> sets all of the Gyros on set ship to set parameters (it is not easy, plainly setting them that way would not make any sense)
/// <param>Yaw</param>, <param>Pitch</param> and <param>Roll</param> correspond 1:1 to all of the Gyroscopes' ones.
/// </summary>
public void MoveAllGyros(float Yaw, float Pitch, float Roll){
    List<IMyGyro> gyros = new List<IMyGyro>();
    GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros);
    foreach(IMyGyro gyro in gyros){
        MoveGyroInAWay(gyro, Yaw, Pitch, Roll);
    }
}

/// <summary> 
/// Method <c>MoveGyroInAWay</c> sets a <param>target</param> Gyro to set parameters (it is not easy, plainly setting it that way would not make any sense)
/// <param>Yaw</param>, <param>Pitch</param> and <param>Roll</param> correspond 1:1 to all of the Gyroscope's ones.
/// </summary>
public void MoveGyroInAWay(IMyGyro target, float Yaw, float Pitch, float Roll){

    switch(TranslateDirection(target)){

        case 13: target.Yaw=    Roll;       target.Pitch=    Yaw;      target.Roll=   Pitch;     break;
        case 14: target.Yaw=    Roll;       target.Pitch=   -Yaw;      target.Roll=  -Pitch;     break;
        case 15: target.Yaw=    Roll;       target.Pitch=   -Pitch;    target.Roll=   Yaw;       break;
        case 16: target.Yaw=    Roll;       target.Pitch=    Pitch;    target.Roll=  -Yaw;       break;
        case 23: target.Yaw=   -Roll;       target.Pitch=   -Yaw;      target.Roll=   Pitch;     break;
        case 24: target.Yaw=   -Roll;       target.Pitch=    Yaw;      target.Roll=  -Pitch;     break;
        case 25: target.Yaw=   -Roll;       target.Pitch=    Pitch;    target.Roll=   Yaw;       break;
        case 26: target.Yaw=   -Roll;       target.Pitch=   -Pitch;    target.Roll=  -Yaw;       break;
        case 31: target.Yaw=    Pitch;      target.Pitch=   -Yaw;      target.Roll=  -Roll;      break;
        case 32: target.Yaw=   -Pitch;      target.Pitch=    Yaw;      target.Roll=   Roll;      break;
        case 35: target.Yaw=   -Pitch;      target.Pitch=   -Roll;     target.Roll=   Yaw;       break;
        case 36: target.Yaw=   -Pitch;      target.Pitch=    Roll;     target.Roll=  -Yaw;       break;
        case 41: target.Yaw=    Pitch;      target.Pitch=    Yaw;      target.Roll=  -Roll;      break;
        case 42: target.Yaw=    Pitch;      target.Pitch=    Yaw;      target.Roll=   Roll;      break;
        case 45: target.Yaw=    Pitch;      target.Pitch=    Roll;     target.Roll=   Yaw;       break;
        case 46: target.Yaw=    Pitch;      target.Pitch=   -Roll;     target.Roll=  -Yaw;       break;
        case 51: target.Yaw=   -Yaw;        target.Pitch=    Pitch;    target.Roll=  -Roll;      break;
        case 52: target.Yaw=   -Yaw;        target.Pitch=   -Pitch;    target.Roll=   Roll;      break;
        case 53: target.Yaw=   -Yaw;        target.Pitch=   -Roll;     target.Roll=   Pitch;     break;
        case 54: target.Yaw=   -Yaw;        target.Pitch=   -Roll;     target.Roll=  -Pitch;     break;
        case 61: target.Yaw=    Yaw;        target.Pitch=   -Pitch;    target.Roll=  -Roll;      break;
        case 62: target.Yaw=    Yaw;        target.Pitch=    Pitch;    target.Roll=   Roll;      break;
        case 63: target.Yaw=    Yaw;        target.Pitch=   -Roll;     target.Roll=   Pitch;     break;
        case 64: target.Yaw=    Yaw;        target.Pitch=   -Roll;     target.Roll=  -Pitch;     break;
     
        default:
            Echo("ERROR: "+target.CustomName+" GYROSCOPE IS IN AN IMPOSSIBLE SETTING.");
            target.ShowOnHUD = true;
        break;

    }

}

/// <summary> 
/// Method <c>ChangeAllGyros</c> sets all of the Gyros' parameter determined by <param>paramIndex</param> to given <param>value</param>
/// paramIndex 0 -> set all to 0
/// paramIndex 1 -> set Yaw to value
/// paramIndex 2 -> set Pitch to value
/// paramIndex 3 -> set Roll to value
/// </summary>
public void ChangeAllGyros(int paramIndex, float value){
    List<IMyGyro> gyros = new List<IMyGyro>();
    GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros);
    foreach(IMyGyro gyro in gyros){
        ChangeAGyro(gyro, paramIndex, value);
    }
}

/// <summary> 
/// Method <c>ChangeAllGyros</c> sets the set <param>target<param>'s parameter determined by <param>paramIndex</param> to given <param>value</param>
/// paramIndex 0 -> set all to 0
/// paramIndex 1 -> set Yaw to value
/// paramIndex 2 -> set Pitch to value
/// paramIndex 3 -> set Roll to value
/// </summary>
public void ChangeAGyro(IMyGyro target, int paramIndex, float value){

    switch(paramIndex){

        case 0:
            target.Pitch = 0; target.Roll = 0; target.Yaw = 0;
        break;

        case 1: 
            switch(TranslateDirection(target)){

                case 13: target.Pitch=  value; break;
                case 14: target.Pitch= -value; break;
                case 15: target.Roll=   value; break;
                case 16: target.Roll=  -value; break;
                case 23: target.Pitch= -value; break;
                case 24: target.Pitch=  value; break;
                case 25: target.Roll=   value; break;
                case 26: target.Roll=  -value; break;
                case 31: target.Pitch= -value; break;
                case 32: target.Pitch=  value; break;
                case 35: target.Roll=   value; break;
                case 36: target.Roll=  -value; break;
                case 41: target.Pitch=  value; break;
                case 42: target.Pitch=  value; break;
                case 45: target.Roll=   value; break;
                case 46: target.Roll=  -value; break;
                case 51: target.Yaw=   -value; break;
                case 52: target.Yaw=   -value; break;
                case 53: target.Yaw=   -value; break;
                case 54: target.Yaw=   -value; break;
                case 61: target.Yaw=    value; break;
                case 62: target.Yaw=    value; break;
                case 63: target.Yaw=    value; break;
                case 64: target.Yaw=    value; break;

                default:
                    Echo("ERROR: "+target.CustomName+" GYROSCOPE IS IN AN IMPOSSIBLE SETTING.");
                    target.ShowOnHUD = true;
                break;
            }
        break;

        case 2: 
            switch(TranslateDirection(target)){

                case 13: target.Roll=   value; break;
                case 14: target.Roll=  -value; break;
                case 15: target.Pitch= -value; break;
                case 16: target.Pitch=  value; break;
                case 23: target.Roll=   value; break;
                case 24: target.Roll=  -value; break;
                case 25: target.Pitch=  value; break;
                case 26: target.Pitch= -value; break;
                case 31: target.Yaw=    value; break;
                case 32: target.Yaw=   -value; break;
                case 35: target.Yaw=   -value; break;
                case 36: target.Yaw=   -value; break;
                case 41: target.Yaw=    value; break;
                case 42: target.Yaw=    value; break;
                case 45: target.Yaw=    value; break;
                case 46: target.Yaw=    value; break;
                case 51: target.Pitch=  value; break;
                case 52: target.Pitch= -value; break;
                case 53: target.Roll=   value; break;
                case 54: target.Roll=  -value; break;
                case 61: target.Pitch= -value; break;
                case 62: target.Pitch=  value; break;
                case 63: target.Roll=   value; break;
                case 64: target.Roll=  -value; break;

                default:
                    Echo("ERROR: "+target.CustomName+" GYROSCOPE IS IN AN IMPOSSIBLE SETTING.");
                    target.ShowOnHUD = true;
                break;

            }
        break;

        case 3: 
            switch(TranslateDirection(target)){

                case 13: target.Yaw=    Roll; break;
                case 14: target.Yaw=    Roll; break;
                case 15: target.Yaw=    Roll; break;
                case 16: target.Yaw=    Roll; break;
                case 23: target.Yaw=   -Roll; break;
                case 24: target.Yaw=   -Roll; break;
                case 25: target.Yaw=   -Roll; break;
                case 26: target.Yaw=   -Roll; break;
                case 31: target.Roll=  -Roll; break;
                case 32: target.Roll=   Roll; break;
                case 35: target.Pitch= -Roll; break;
                case 36: target.Pitch=  Roll; break;
                case 41: target.Roll=  -Roll; break;
                case 42: target.Roll=   Roll; break;
                case 45: target.Pitch=  Roll; break;
                case 46: target.Pitch= -Roll; break;
                case 51: target.Roll=  -Roll; break;
                case 52: target.Roll=   Roll; break;
                case 53: target.Pitch= -Roll; break;
                case 54: target.Pitch= -Roll; break;
                case 61: target.Roll=  -Roll; break;
                case 62: target.Roll=   Roll; break;
                case 63: target.Pitch= -Roll; break;
                case 64: target.Pitch= -Roll; break;

                default:
                    Echo("ERROR: "+target.CustomName+" GYROSCOPE IS IN AN IMPOSSIBLE SETTING.");
                    target.ShowOnHUD = true;
                break;

            }
        break;

        default: 
            Echo("ERROR: WRONG PARAMETER.");
        break;

    }

}

/// <summary> 
/// Method <c>GetControllingBlock</c> attempts to set the MISSILE_CONTROLLER to the first working controller it can find
/// </summary>
public void GetControllingBlock(){
    List<IMyShipController> controls = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(controls);

    MISSILE_CONTROLLER = null;

    foreach(IMyShipController controler in controls){

        if(MISSILE_CONTROLLER == null && controler.IsWorking) MISSILE_CONTROLLER = controler;

    }
}

/// <summary> 
/// Method <c>GetSpeed</c> returns <returns>ship's speed</returns> that the program gets from the MISSILE_CONTROLLER
/// </summary>
public double GetSpeed(){
    if(MISSILE_CONTROLLER == null) return -1D;
    else{
        return MISSILE_CONTROLLER.GetShipSpeed();
    }
}

/// <summary> 
/// Method <c>FindThrusters</c> attempts to find, check the direction and rename every thruster on the programming block's grid.
/// </summary>
public void FindThrusters(){

    ClearMissileThrusters();
    Echo("Attempting to find thrusters...");
    List<IMyThrust> output = new List<IMyThrust>();
    GridTerminalSystem.GetBlocksOfType<IMyThrust> (output);
    foreach (IMyThrust t in output){

        int dirint = TranslateDirection(t);
        t.CustomName=DirintToName(dirint);
        switch(dirint){

        case 1: MISSILE_FORWARD  .Add(t); break;
        case 2: MISSILE_BACKWARD .Add(t); break;
        case 3: MISSILE_LEFT     .Add(t); break;
        case 4: MISSILE_RIGHT    .Add(t); break;
        case 5: MISSILE_UP       .Add(t); break;
        case 6: MISSILE_DOWN     .Add(t); break;  
        default: break;

        }

    }
    bool ok = true;
    if(MISSILE_FORWARD .Count==0) {ok = false; Echo("WARNING: THERE ARE NO FORWARD THRUSTERS ON THIS MISSILE.");}
    if(MISSILE_BACKWARD.Count==0) {ok = false; Echo("WARNING: THERE ARE NO BACKWARD THRUSTERS ON THIS MISSILE. CURRENT VERSION OF THE MISSILE OS MAY NOT BE ABLE TO PERFORM CORRECTLY.");}
    if(MISSILE_LEFT    .Count==0) {ok = false; Echo("WARNING: THERE ARE NO LEFT THRUSTERS ON THIS MISSILE.");}
    if(MISSILE_RIGHT   .Count==0) {ok = false; Echo("WARNING: THERE ARE NO RIGHT THRUSTERS ON THIS MISSILE.");}
    if(MISSILE_UP      .Count==0) {ok = false; Echo("WARNING: THERE ARE NO UP THRUSTERS ON THIS MISSILE.");}
    if(MISSILE_DOWN    .Count==0) {ok = false; Echo("WARNING: THERE ARE NO DOWN THRUSTERS ON THIS MISSILE.");}
    if(ok) Echo("All thrusters found.");

}

/// <summary> 
/// Method <c>ChangeMode</c> attempts to change the CurrentMode of the Missile's OS from the <param>string ModeName</param>.
/// </summary>
public void ChangeMode(string ModeName){

    Echo("Changing mode from "+ CurrentMode +" to "+ ModeName +".");
    switch (ModeName){

        case "INIT":
            CurrentMode = "INIT";
            MIN_SENSOR_EXTEND =  1F;
            MAX_SENSOR_EXTEND =  5F;
            SetSensor(MainSensorName  , 1F , 1F , 1F , 1F , 1F , 1F , false);
            SetSensor(LeftSensorName  , 1F , 1F , 1F , 1F , 1F , 1F , false);
            SetSensor(RightSensorName , 1F , 1F , 1F , 1F , 1F , 1F , false);
            SetSensor(TopSensorName   , 1F , 1F , 1F , 1F , 1F , 1F , false);
            SetSensor(BottomSensorName, 1F , 1F , 1F , 1F , 1F , 1F , false);
            ChangeMode("LAUNCH");
        break;

        case "LAUNCH":
            STARTING_SPEED = GetSpeed();
            MAX_SPEED = STARTING_SPEED + 10D;
            Echo("Declaring Max Speed as: "+ MAX_SPEED);
            MIN_SENSOR_EXTEND =  3F;
            MAX_SENSOR_EXTEND = 20F;
            SetSensor(MainSensorName  ,  2F,  2F,  2F,  2F, 30F, 2F, false);
            SetSensor(LeftSensorName  , 30F,  1F,  1F,  1F, 30F, 2F, false);
            SetSensor(RightSensorName ,  1F, 30F,  1F,  1F, 30F, 2F, false);
            SetSensor(TopSensorName   ,  1F,  1F, 30F,  1F, 30F, 2F, false);
            SetSensor(BottomSensorName,  1F,  1F,  1F, 30F, 30F, 2F, false);
            CurrentMode = "LAUNCH";
        break;

        case "SEEK":
            MoveAGroupThrusters(MISSILE_FORWARD , 0F);  EnableAGroupThrusters(MISSILE_FORWARD , false);
            MoveAGroupThrusters(MISSILE_BACKWARD, 0F);  EnableAGroupThrusters(MISSILE_BACKWARD, false);  
            MoveAGroupThrusters(MISSILE_LEFT    , 0F);  EnableAGroupThrusters(MISSILE_LEFT    , false);
            MoveAGroupThrusters(MISSILE_RIGHT   , 0F);  EnableAGroupThrusters(MISSILE_RIGHT   , false);
            MoveAGroupThrusters(MISSILE_UP      , 0F);  EnableAGroupThrusters(MISSILE_UP      , false);
            MoveAGroupThrusters(MISSILE_DOWN    , 0F);  EnableAGroupThrusters(MISSILE_DOWN    , false);

            MainSensor  .DetectSmallShips = false; 
            LeftSensor  .DetectSmallShips = false; 
            RightSensor .DetectSmallShips = false;
            TopSensor   .DetectSmallShips = false;  
            BottomSensor.DetectSmallShips = false;
            
            MIN_SENSOR_EXTEND = 30F;
            MAX_SENSOR_EXTEND = 10000F;
            SetSensor(MainSensorName  , 10F, 10F, 10F, 10F, MAX_SENSOR_EXTEND, 2F, true);
            SetSensor(LeftSensorName  , MAX_SENSOR_EXTEND,  1F, 20F, 20F, MAX_SENSOR_EXTEND, 2F, true);
            SetSensor(RightSensorName ,  1F, MAX_SENSOR_EXTEND, 20F, 20F, MAX_SENSOR_EXTEND, 2F, true);
            SetSensor(TopSensorName   , 20F, 20F, MAX_SENSOR_EXTEND,  1F, MAX_SENSOR_EXTEND, 2F, true);
            SetSensor(BottomSensorName, 20F, 20F,  1F, MAX_SENSOR_EXTEND, MAX_SENSOR_EXTEND, 2F, true);
            CurrentMode = "SEEK";
        break;

        case "CALIBRATE":
            MIN_SENSOR_EXTEND = 30F;
            MAX_SENSOR_EXTEND = 10000F;
            SetSensor(MainSensorName  , 10F, 10F, 10F, 10F, MAX_SENSOR_EXTEND, 2F, true);
            SetSensor(LeftSensorName  , 50F,  1F, 20F, 20F, MAX_SENSOR_EXTEND, 2F, true);
            SetSensor(RightSensorName ,  1F, 50F, 20F, 20F, MAX_SENSOR_EXTEND, 2F, true);
            SetSensor(TopSensorName   , 20F, 20F, 50F,  1F, MAX_SENSOR_EXTEND, 2F, true);
            SetSensor(BottomSensorName, 20F, 20F,  1F, 50F, MAX_SENSOR_EXTEND, 2F, true);
            CurrentMode = "CALIBRATE";
        break;

        case "CLOSEIN":
            EnableAGroupThrusters(MISSILE_FORWARD , true);
            EnableAGroupThrusters(MISSILE_BACKWARD, true);
            EnableAGroupThrusters(MISSILE_LEFT    , true);
            EnableAGroupThrusters(MISSILE_RIGHT   , true);
            EnableAGroupThrusters(MISSILE_UP      , true);
            EnableAGroupThrusters(MISSILE_DOWN    , true);
            CurrentMode = "CLOSEIN";
        break;

        default:
            Echo("Function 'ChangeMode': Undefined input value: "+ ModeName);
        break;

    }

}

/// MAIN FUNCTIONS

/// <summary> 
/// Method <c>EvaluateSensors</c> uses CurrentMode and Sensor's input to coordinate the Missile's actions and change it's CurrentMode.
/// </summary>
public void EvaluateSensors(){

    if(CurrentMode == "INIT")       return;
    else if(CurrentMode == "LAUNCH"){

        MISSILE_CONTROLLER.DampenersOverride = true;

        if(!MainSensor.IsActive){

            if(GetSpeed()<MAX_SPEED){

                EnableAGroupThrusters(MISSILE_BACKWARD, false);
                EnableAGroupThrusters(MISSILE_FORWARD, true);
                MoveAGroupThrusters(MISSILE_FORWARD, 1F);

            }
            else if(WasJustBraking){
                if(GetSpeed()<10D||GetSpeed()>MAX_SPEED+20D){
                    WasJustBraking = false;
                    EnableAGroupThrusters(MISSILE_FORWARD, true);
                }
                else{
                    EnableAGroupThrusters(MISSILE_BACKWARD, false);
                    EnableAGroupThrusters(MISSILE_FORWARD, true);
                    MoveAGroupThrusters(MISSILE_FORWARD, 1F);
                }
            }
            else{

                MoveAGroupThrusters(MISSILE_FORWARD, 0F);

            }
            MoveAGroupThrusters(MISSILE_BACKWARD, 0F);
        }
        else{

            EnableAGroupThrusters(MISSILE_BACKWARD, true);
            EnableAGroupThrusters(MISSILE_FORWARD, false);
            MoveAGroupThrusters(MISSILE_FORWARD, 0F);
            MoveAGroupThrusters(MISSILE_BACKWARD, 1F);
            WasJustBraking = true;

        }

        if( LeftSensor.IsActive &&  RightSensor.IsActive){

            ChangeSensorExtend(LeftSensor,-(1F));
            ChangeSensorExtend(RightSensor,-(1F));
            MoveAGroupThrusters(MISSILE_LEFT, 0F);
            MoveAGroupThrusters(MISSILE_RIGHT, 0F);

        }
        else
        if( LeftSensor.IsActive && !RightSensor.IsActive){

            MoveAGroupThrusters(MISSILE_LEFT, 0F);
            MoveAGroupThrusters(MISSILE_RIGHT, 0.33F);

        }
        else
        if(!LeftSensor.IsActive &&  RightSensor.IsActive){
            
            MoveAGroupThrusters(MISSILE_LEFT, 0.33F);
            MoveAGroupThrusters(MISSILE_RIGHT, 0F);

        }
        else{

            ChangeSensorExtend(LeftSensor,1F);
            ChangeSensorExtend(RightSensor,1F);
            MoveAGroupThrusters(MISSILE_LEFT, 0F);
            MoveAGroupThrusters(MISSILE_RIGHT, 0F);

        }
        
        if( TopSensor.IsActive &&  BottomSensor.IsActive){

            ChangeSensorExtend(TopSensor,-(1F));
            ChangeSensorExtend(BottomSensor,-(1F));
            MoveAGroupThrusters(MISSILE_UP, 0F);
            MoveAGroupThrusters(MISSILE_DOWN, 0F);
        }
        else
        if( TopSensor.IsActive && !BottomSensor.IsActive){

            MoveAGroupThrusters(MISSILE_UP, 0F);
            MoveAGroupThrusters(MISSILE_DOWN, 0.33F);

        }
        else
        if(!TopSensor.IsActive &&  BottomSensor.IsActive){

            MoveAGroupThrusters(MISSILE_UP, 0.33F);
            MoveAGroupThrusters(MISSILE_DOWN, 0F);

        }
        else{

            ChangeSensorExtend(TopSensor,1F);
            ChangeSensorExtend(BottomSensor,1F);
            MoveAGroupThrusters(MISSILE_UP, 0F);
            MoveAGroupThrusters(MISSILE_DOWN, 0F);

        }

    }
    else if(CurrentMode == "SEEK"){

        MISSILE_CONTROLLER.DampenersOverride = false;
        
        if(!MainSensor.IsActive){

            if     ( LeftSensor.IsActive && RightSensor.IsActive && TopSensor.IsActive && BottomSensor.IsActive) MoveAllGyros( 20F, 20F, 20F);
            else if( LeftSensor.IsActive && RightSensor.IsActive && TopSensor.IsActive &&!BottomSensor.IsActive) MoveAllGyros(  0F, 20F,  0F);
            else if( LeftSensor.IsActive && RightSensor.IsActive &&!TopSensor.IsActive && BottomSensor.IsActive) MoveAllGyros(  0F,-20F,  0F);
            else if( LeftSensor.IsActive && RightSensor.IsActive &&!TopSensor.IsActive &&!BottomSensor.IsActive) MoveAllGyros(  0F, 20F,  0F);
            else if( LeftSensor.IsActive &&!RightSensor.IsActive && TopSensor.IsActive && BottomSensor.IsActive) MoveAllGyros(-20F,  0F,  0F);
            else if( LeftSensor.IsActive &&!RightSensor.IsActive && TopSensor.IsActive &&!BottomSensor.IsActive) MoveAllGyros(-20F, 20F,  0F);
            else if( LeftSensor.IsActive &&!RightSensor.IsActive &&!TopSensor.IsActive && BottomSensor.IsActive) MoveAllGyros(-20F,-20F,  0F);
            else if( LeftSensor.IsActive &&!RightSensor.IsActive &&!TopSensor.IsActive &&!BottomSensor.IsActive) MoveAllGyros(-20F,  0F,  0F);
            else if(!LeftSensor.IsActive && RightSensor.IsActive && TopSensor.IsActive && BottomSensor.IsActive) MoveAllGyros( 20F,  0F,  0F);
            else if(!LeftSensor.IsActive && RightSensor.IsActive && TopSensor.IsActive &&!BottomSensor.IsActive) MoveAllGyros(-20F, 20F,  0F);
            else if(!LeftSensor.IsActive && RightSensor.IsActive &&!TopSensor.IsActive && BottomSensor.IsActive) MoveAllGyros(-20F,-20F,  0F);
            else if(!LeftSensor.IsActive && RightSensor.IsActive &&!TopSensor.IsActive &&!BottomSensor.IsActive) MoveAllGyros(-20F,  0F,  0F);
            else if(!LeftSensor.IsActive &&!RightSensor.IsActive && TopSensor.IsActive && BottomSensor.IsActive) MoveAllGyros( 20F,  0F,  0F);
            else if(!LeftSensor.IsActive &&!RightSensor.IsActive && TopSensor.IsActive &&!BottomSensor.IsActive) MoveAllGyros(  0F, 20F,  0F);
            else if(!LeftSensor.IsActive &&!RightSensor.IsActive &&!TopSensor.IsActive && BottomSensor.IsActive) MoveAllGyros(  0F,-20F,  0F);
            else { Echo("NO TARGET FOUND"); ChangeMode("INIT"); }
            
        }
        else{

            ChangeAllGyros(0,0);
            ChangeMode("CALIBRATE");

        }

    }
    else if(CurrentMode == "CALIBRATE"){

        MISSILE_CONTROLLER.DampenersOverride = false;

        if     (!LeftSensor.IsActive && RightSensor) MoveAllGyros( 10F,  0F,  0F);
        else if( LeftSensor.IsActive &&!RightSensor) MoveAllGyros(-10F,  0F,  0F);

        if     (!TopSensor.IsActive && BottomSensor) MoveAllGyros(  0F, 10F,  0F);
        else if( TopSensor.IsActive &&!BottomSensor) MoveAllGyros(  0F,-10F,  0F);

        if(MainSensor.IsActive){
            if(FoundDistance)   {
                ChangeMode("CLOSEIN");
                STARTING_DISTANCE = MainSensor.FrontExtend;
                LeftSensor  .FrontExtend = STARTING_DISTANCE;
                RightSensor .FrontExtend = STARTING_DISTANCE;
                TopSensor   .FrontExtend = STARTING_DISTANCE;
                BottomSensor.FrontExtend = STARTING_DISTANCE;
            }
            else                ChangeForwardExtend(MainSensor,-10F);
        }
        else{
            FoundDistance = true;
            ChangeForwardExtend(MainSensor, 10F);
        }

    }
    else if(CurrentMode == "CLOSEIN"){

        if(GetSpeed()<MAX_SPEED){

            EnableAGroupThrusters(MISSILE_BACKWARD, false);
            EnableAGroupThrusters(MISSILE_FORWARD, true);
            MoveAGroupThrusters(MISSILE_FORWARD, 1F);

        }
        else{

            MoveAGroupThrusters(MISSILE_FORWARD, 0F);

        }

        MISSILE_CONTROLLER.DampenersOverride = true;
        if     (!LeftSensor.IsActive && RightSensor) MoveAllGyros( 10F,  0F,  0F);
        else if( LeftSensor.IsActive &&!RightSensor) MoveAllGyros(-10F,  0F,  0F);

        if     (!TopSensor.IsActive && BottomSensor) MoveAllGyros(  0F, 10F,  0F);
        else if( TopSensor.IsActive &&!BottomSensor) MoveAllGyros(  0F,-10F,  0F);

    }

}

public void Main(string argument, UpdateType updateSource){

    if(MISSILE_CONTROLLER==null || !MISSILE_CONTROLLER.IsWorking) GetControllingBlock();
    if(argument=="INIT" || argument=="init" || argument=="LAUNCH" || argument=="launch") {
        
        InitMissile();
        ChangeMode("INIT");

    }
    else{

        EvaluateSensors();

    }

}