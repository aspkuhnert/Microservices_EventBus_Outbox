using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildingBlocks.Database.Tests
{
   internal static class Utility
   {
      public static IConfigurationRoot GetTestConfigFromFile()
      {
         return new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
      }
   }
}
