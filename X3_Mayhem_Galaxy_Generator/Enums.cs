using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace X3_Mayhem_Galaxy_Generator
{
    public enum ERelationship
    {
        Dynamic = 0,
        PermanentAlliance = 1,
        PermanentWar = -1
    }

    public enum ECompany
    {
        None = -1,
        Markus = 0,     // Argon
        Oceana = 1,     // Boron
        Pontifex = 2,   // Paranid
        Thurok = 3,     // Split
        Salecrest = 4,  // Teladi
        Industritech = 5    // Terran
    }

    public enum EFood
    {
        Fish = 0,
        Fruit = 1,
        Meat = 2,
        Vegetables = 3
    }

    public enum ENodeType
    {
        Sector = 1,
        Background = 2,
        Sun = 3,
        Planet = 4,
        Asteroid = 17,
        Gate = 18,
        Special = 20
    }


    public enum EGalaxyExpansion
    {
        Origin = -1,         // 1 system per race, other than Xenon.
        Early = 0,          // 50% unknown space 
        Average = 1,        // 30% unknown space
        Advanced = 2        // 10% unknown space
    }

    // This order must remain unchanged.
    public enum ERace
    {
        None = 0,
        Argon = 1,
        Boron = 2,
        Split = 3,
        Paranid = 4,
        Teladi = 5,
        Xenon = 6,
        Unknown = 14,
        Terran = 18
    }

    public enum EQuadrant
    {
        None = 0,
        UpperLeft = 1,
        UpperRight = 2,
        LowerLeft = 3,
        LowerRight = 4,
        MiddleLeft = 5,
        MiddleRight = 6
    }

    public enum EGateDirection
    {
        North = 0,
        South = 1,
        West = 2,
        East = 3
    }

    public enum EGateDensity
    {
        Low,
        High
    }

}
