using RapidRepo.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidRepo.Tests.TestData;

internal interface ICompanyRepository : IRepository<Company, Guid>
{
}
