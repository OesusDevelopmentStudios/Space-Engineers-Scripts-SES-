// UTILITY FUNCTIONS

public List<IMyAirtightHangarDoor> getInternalDoors(){
    /*
        Drzwi, które równocześnie umożliwiają transport pomiędzy lewym i prawym hangarem oraz
        umożliwiają utrzymanie jednego hangaru pod ciśnieniem, a drugiego przeciwnie.
     */
    List<IMyAirtightHangarDoor> Doors = new List<IMyAirtightHangarDoor>();
    List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName("[]", temp); //TODO: ogarnięcie  zeby ta nazwa aktualnie miała jakiś sens

    foreach (IMyTerminalBlock b in temp)
    {
        if (b is IMyAirtightHangarDoor)
        {
            IMyAirtightHangarDoor tempo = b as IMyAirtightHangarDoor;
            Doors.Add(tempo);
        }
    }
    temp.Clear();

    return Doors;
}

public List<IMyDoor> getLeftEntranceDoors(){
    /*
        Drzwi prowadzące z hangaru do reszty okrętu. Podczas zmiany stanu lewych drzwi hangaru na "Open",
        powinny zostać zamknięte, by nie zdepressurize'ować całości okrętu.
     */
    List<IMyDoor> Doors = new List<IMyDoor>();
    List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName("[]", temp); //TODO: ogarnięcie  zeby ta nazwa aktualnie miała jakiś sens

    foreach (IMyTerminalBlock b in temp)
    {
        if (b is IMyDoor)
        {
            IMyDoor tempo = b as IMyDoor;
            Doors.Add(tempo);
        }
    }
    temp.Clear();

    return Doors;
}

public List<IMyDoor> getRightEntranceDoors(){
    /*
        Drzwi prowadzące z hangaru do reszty okrętu. Podczas zmiany stanu prawych drzwi hangaru na "Open",
        powinny zostać zamknięte, by nie zdepressurize'ować całości okrętu.
     */
    List<IMyDoor> Doors = new List<IMyDoor>();
    List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName("[]", temp); //TODO: ogarnięcie  zeby ta nazwa aktualnie miała jakiś sens

    foreach (IMyTerminalBlock b in temp)
    {
        if (b is IMyDoor)
        {
            IMyDoor tempo = b as IMyDoor;
            Doors.Add(tempo);
        }
    }
    temp.Clear();

    return Doors;
}

public List<IMyAirVent> getMainOxygenOutlets(){
    /*
        Outlety z głównej rezerwy tlenu na okręcie, powinny włączyć się jako drugie, po
        tym, gdy hangarowe pojemniki z tlenem wypuszczą już absolutnie wszystko. 
     */
    List<IMyAirVent> output = new List<IMyAirVent>();
    List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName("[]", temp); //TODO: ogarnięcie  zeby ta nazwa aktualnie miała jakiś sens

    foreach (IMyTerminalBlock b in temp)
    {
        if (b is IMyAirVent)
        {
            IMyAirVent tempo = b as IMyAirVent;
            output.Add(tempo);
        }
    }
    temp.Clear();

    return output;
}

public List<IMyAirVent> getReserveOxygenOutlets(){
    /*
        Outlety ze specjalnej, hangarowej rezerwy tlenu, mają jako pierwsze jak najszybciej wypełnić hangary
        tlenem
     */
    List<IMyAirVent> output = new List<IMyAirVent>();
    List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName("[]", temp); //TODO: ogarnięcie  zeby ta nazwa aktualnie miała jakiś sens

    foreach (IMyTerminalBlock b in temp)
    {
        if (b is IMyAirVent)
        {
            IMyAirVent tempo = b as IMyAirVent;
            output.Add(tempo);
        }
    }
    temp.Clear();

    return output;
}

public List<IMyOxygenTank> getOxygenReserve(){
    /*
        Zbiorniki z tlenem umieszczone pod mostkiem, odłączone od reszty systemu logistycznego okrętu.
        Stworzone w ten sposób, żeby uprościć algorytm/skrypt do minimum.
        Nie powinny posiadać żadnego dodatkowego systemu tworzenia tlenu, bo ich przeznaczeniem jest 
        być pustymi przez większość czasu
     */
    List<IMyOxygenTank> output = new List<IMyOxygenTank>();
    List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName("[]", temp); //TODO: ogarnięcie  zeby ta nazwa aktualnie miała jakiś sens

    foreach (IMyTerminalBlock b in temp)
    {
        if (b is IMyOxygenTank)
        {
            IMyOxygenTank tempo = b as IMyOxygenTank;
            output.Add(tempo);
        }
    }
    temp.Clear();

    return output;
}


public void Main(string argument, UpdateType updateSource){
    DefaultLook();

    switch (argument)
    {
        default:
            AlertOutput("UNKNOWN COMMAND");
            break;
    }
}