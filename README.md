EFEagerLoad
===========

EFEagerLoad allows you to easily and selectively eager load your Entity Framework entities (Collections and straight entities) into isolated project models using your own reusable mapping functions. 

## Contents
- [License](#license)
- [About the AweSam.Net Project] (#About-the-AweSamNet-Project)
- [Setup] (#Setup)
- [Usage] (#Usage)

## License
See license details [here](/LICENSE.md).

## About the AweSam.Net Project

AweSam.Net is a project dedicated to contributing to the OSS world as well as local communities. 
Follow the AweSamNet project on Twitter: <a href="https://twitter.com/AweSamNet" target="_blank">@AweSamNet</a> and be sure to like on <a href="http://facebook.com/AweSamNet" target="_blank">facebook</a>, where you can also get in touch with us.

## Setup

First you'll need to setup your mapping profiles.  A mapping profile provides you with a set of mappings that can be used to map a model to different entities based on the need. For example:

1. YourModels.Invoice --> Db.Invoice
2. YourModels.Invoice --> Db.ArchivedInvoice

In this case, the default profile might handle the mapping of case 1 while a separate profile (Ex: "Archives") might handle the mapping of case 2.

This is achieved as follows:

    //Default profile
    EFEagerLoad.AddEFMapping<Models.User, EFData.Db.User>(x => x.Invoices, x => x.Invoices);
    EFEagerLoad.AddEFMapping<Models.Invoice, EFData.Db.Invoice>(x => x.InvoiceLines, x => x.InvoiceLines);
	
	//Now the Archives profile
    EFEagerLoad.AddEFMapping<Models.User, EFData.Db.ArchivedUser>(x => x.Invoices, x => x.ArchivedInvoices, "Archives");
    EFEagerLoad.AddEFMapping<Models.Invoice, EFData.Db.ArchivedInvoice>(x => x.InvoiceLines, x => x.ArchivedInvoiceLines, "Archives");

You will need to put this code somewhere where it will only be executed once (perhaps Global.asax) as you are not able to have a selector mapped more than once on a given profile.  Otherwise, this would make it impossible to determine which entity you wish to eager load with the following selector:

    x => ((Models.Invoice)x).InvoiceLines);
    
## Usage

Once set up, using the class is as simple as this:

    public class TestRepository
    {
        public IEnumerable<Models.Invoice>GetUsersTest()
        {
            using (var db = new DbContext())
			{
                //Active Users and Data
                var query = from x in db.Users
                            select x;
            
                //get users with no eager laoding
                var usersNoEager = query.SelectWithEager(Map);
                                
                //get users with invoices no lines
                var usersWithInvoices = query.SelectWithEager(Map, x => ((Models.User)x).Invoices); //Eager load Invoices
                
                //get users with invoices and lines
                var usersWithInvoicesAndLines = query.SelectWithEager(Map
                                                                      , x => ((Models.User)x).Invoices //Eager load Invoices
                                                                      , x => ((Models.Invoice)x).InvoiceLines); //Eager load InvoiceLines
                
                //Archived Users and Data
                var query = from x in db.ArchivedUsers
                            select x;
            
                //get archived users with archived invoices and archived lines, using the "Archives" mapping profile.
                var archivedUsersWithInvoicesAndLines = query.SelectWithEager("Archives", Map
                                                                              , x => ((Models.User)x).Invoices //Eager load Archived Invoices
                                                                              , x => ((Models.Invoice)x).InvoiceLines); //Eager load Archived InvoiceLines
            }
        }

        private IEnumerable<Models.User> Map(IEnumerable<EFData.Db.User> query)
        {
            return from x in query
                   select new User
                   {
                       ID = x.ID,
					   UserName = x.UserName,
					   Invoices = Map(x.Invoices)
                   };
        }

        private IEnumerable<Models.User> Map(IEnumerable<EFData.Db.ArchivedUser> query)
        {
            return from x in query
                   select new User
                   {
                       ID = x.ID,
					   UserName = x.UserName,
                       Invoices = Map(x.ArchivedInvoices)
                   };
        }

        private IEnumerable<Models.Invoice> Map(IEnumerable<EFData.Db.Invoice> query)
        {
            return from x in query
                   select new Invoice
                   {
                       ID = x.ID,
                       CreateDate = x.CreateDate,
                       Total = x.Total,
                       InvoiceLines = Map(x.InvoiceLines)
                   };
        }

        private IEnumerable<Models.Invoice> Map(IEnumerable<EFData.Db.ArchivedInvoice> query)
        {
            return from x in query
                   select new Invoice
                   {
                       ID = x.ID,
                       CreateDate = x.CreateDate,
                       Total = x.Total,
                       InvoiceLines = Map(x.ArchivedInvoiceLines)
                   };
        }

        private IEnumerable<Models.InvoiceLine> Map(IEnumerable<EFData.Db.InvoiceLine> query)
        {
            return from x in query
                   select new InvoiceLine
                   {
                       ID = x.ID,
                       Total = x.Total,
                       Description = x.Description
                   };
        }
        
        private IEnumerable<Models.InvoiceLine> Map(IEnumerable<EFData.Db.ArchivedInvoiceLine> query)
        {
            return from x in query
                   select new Invoice
                   {
                       ID = x.ID,
                       Total = x.Total,
                       Description = x.Description
                   };
        }
    }

