﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using BiberDAMM.Models;

namespace BiberDAMM.Helpers
{
    //class for using constant variables that are used over the whole application but can not be changed [KrabsJ]
    public static class ConstVariables
    {
        public const string RoleAdministrator = "Administrator";
        public const string RoleDoctor = "Arzt";
        public const string RoleNurse = "Pflegekraft";
        public const string RoleCleaner = "Reinigungskraft";
        public const string RoleTherapist = "Therapeut";
    }
}