﻿using System;
using System.Data.Entity;
using System.Linq;
using System.Text;
using Breeze.ContextProvider;
using Breeze.ContextProvider.EF6;
using Newtonsoft.Json.Linq;
using Northwind.Models;

namespace DocCode.DataAccess
{
    /// <summary>
    /// Repository (a "Unit of Work" really) of Northwind models.
    /// </summary>
    public class NorthwindRepository
    {
        public NorthwindRepository()
        {
            _contextProvider = new EFContextProvider<NorthwindContext>();
        }

        public string Metadata
        {
            get
            {
                // Returns metadata from a dedicated DbContext that is different from
                // the DbContext used for other operations
                // See NorthwindMetadataContext for more about the scenario behind this.
                var metaContextProvider = new EFContextProvider<NorthwindMetadataContext>();
                return metaContextProvider.Metadata();
            }
        }

        public SaveResult SaveChanges(JObject saveBundle)
        {
            PrepareSaveGuard();
            return _contextProvider.SaveChanges(saveBundle);
        }

        public IQueryable<Category> Categories {
          get { return Context.Categories; }
        }

        public IQueryable<Customer> Customers
        {
            get { return ForCurrentUser(Context.Customers); }
        }

        public IQueryable<Customer> CustomersAndOrders
        {
            get { return ForCurrentUser(Context.Customers).Include("Orders"); }
        }

        //http://stackoverflow.com/questions/22491332/breeze-filtering-expand-on-server-side
        private class CustomerDto : Customer { } // EF requires a shadow class to make the LINQ query work
        public IQueryable<Customer> CustomersAnd1998Orders {
          get
          {
            return ForCurrentUser(Context.Customers)
            .Select(c => new CustomerDto {
              CustomerID = c.CustomerID,
              CompanyName =  c.CompanyName,
              ContactName =  c.ContactName,
              ContactTitle = c.ContactTitle,
              Address = c.Address,
              City = c.City,
              Region = c.Region,
              PostalCode = c.PostalCode,
              Country = c.Country,
              Phone =  c.Phone,
              Fax = c.Fax,
              RowVersion = c.RowVersion,

              Orders = c.Orders
                        .Where(o =>  o.OrderDate != null && o.OrderDate.Value.Year == 1998)
                        .ToList()
            });
          }
        }

        public IQueryable<Customer> CustomersStartingWithA {
          get {
            return ForCurrentUser(Context.Customers)
                .Where(c => c.CompanyName.StartsWith("A"));
          }
        }

        public IQueryable<Customer> CustomersWithFilterOptions(JObject options)
        {
          var query = ForCurrentUser(Context.Customers);
          if (options == null) { return query; }

          if (options["CompanyName"] != null)
          {
            var companyName = (string) options["CompanyName"];
            if (!String.IsNullOrEmpty(companyName))
            {
              query = query.Where(c => c.CompanyName == companyName);
            }
          }

          if (options["Ids"] != null)
          {
            var ids = options["Ids"].Select(id => (Guid) id).ToList();
            if (ids.Count > 0)
            {
              query = query.Where(c => ids.Contains(c.CustomerID));
            }
          }

          return query;
        }

        public IQueryable<Employee> Employees {
          get { return ForCurrentUser(Context.Employees); }
        }

        public IQueryable<EmployeeTerritory> EmployeeTerritories {
          get { return Context.EmployeeTerritories; }
        }

        public IQueryable<Order> OrdersForProduct(int productID = 0)
          {
              var query = ForCurrentUser(Context.Orders);

              query = query.Include("Customer").Include("OrderDetails");

              return (productID == 0)
                          ? query
                          : query.Where(o => o.OrderDetails.Any(od => od.ProductID == productID));
          }

        public IQueryable<Order> Orders
        {
            get { return ForCurrentUser(Context.Orders); }
        }

        public IQueryable<InternationalOrder> InternationalOrders
        {
            get { return ForCurrentUser(Context.InternationalOrders); }
        }

        public IQueryable<Order> OrdersAndCustomers
        {
            get { return ForCurrentUser(Context.Orders).Include("Customer"); }
        }

        public IQueryable<Order> OrdersAndDetails
        {
            get { return ForCurrentUser(Context.Orders).Include("OrderDetails"); }
        }

        public IQueryable<OrderDetail> OrderDetails
        {
            get { return ForCurrentUser(Context.OrderDetails); }
        }

        public IQueryable<Product> Products
        {
          get { return ForCurrentUser(Context.Products); }
        }

        public IQueryable<Region> Regions
        {
            get { return Context.Regions; }
        }

        public IQueryable<Supplier> Suppliers
        {
            get { return Context.Suppliers; }
        }

        public IQueryable<Territory> Territories
        {
            get { return Context.Territories; }
        }

        // Demonstrate a "View Entity" a selection of "safe" entity properties
        // UserPartial is not in Metadata and won't be client cached unless
        // you define metadata for it on the Breeze client
        public IQueryable<UserPartial> UserPartials
        {
            get
            {
                return ForCurrentUser(Context.Users)
                              .Select(u => new UserPartial
                                  {
                                      Id = u.Id,
                                      UserName = u.UserName,
                                      FirstName = u.FirstName,
                                      LastName = u.LastName
                                      // Even though this works, sending every user's roles seems unwise
                                      // Roles = user.UserRoles.Select(ur => ur.Role)
                                  });
            }
        }

        // Useful when need ONE user and its roles
        // Could further restrict to the authenticated user
        public UserPartial GetUserById(int id)
        {
            return ForCurrentUser(Context.Users)
                .Where(u => u.Id == id )
                .Select(u => new UserPartial
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    Roles = u.UserRoles.Select(ur => ur.Role)
                })
                .FirstOrDefault();
        }

        public string Reset(string options)
        {

            // If full reset, delete all additions to the database
            // else delete additions made during this user's session
            var where = options.Contains("fullreset")
                ? "IS NOT NULL"
                : ("= '" + UserSessionId + "'");

            var deleted = new StringBuilder("reset deleted: ");

            var deleteSql = "DELETE FROM [CUSTOMER] WHERE [USERSESSIONID] " + where;
            var count = Context.Database.ExecuteSqlCommand(deleteSql);
            deleted.Append(count + " Customers; ");

            deleteSql = "DELETE FROM [EMPLOYEE] WHERE [USERSESSIONID] " + where;
            count = Context.Database.ExecuteSqlCommand(deleteSql);
            deleted.Append(count + " Employees; ");

            deleteSql = "DELETE FROM [PRODUCT] WHERE [USERSESSIONID] " + where;
            count = Context.Database.ExecuteSqlCommand(deleteSql);
            deleted.Append(count + " Products; ");

            deleteSql = "DELETE FROM [ORDERDETAIL] WHERE [USERSESSIONID] " + where;
            count = Context.Database.ExecuteSqlCommand(deleteSql);
            deleted.Append(count + " OrderDetails; ");

            deleteSql = "DELETE FROM [INTERNATIONALORDER] WHERE [USERSESSIONID] " + where;
            count = Context.Database.ExecuteSqlCommand(deleteSql);
            deleted.Append(count + " InternationalOrders; ");

            deleteSql = "DELETE FROM [ORDER] WHERE [USERSESSIONID] " + where;
            count = Context.Database.ExecuteSqlCommand(deleteSql);
            deleted.Append(count + " Orders; ");

            deleteSql = "DELETE FROM [USER] WHERE [USERSESSIONID] " + where;
            count = Context.Database.ExecuteSqlCommand(deleteSql);
            deleted.Append(count + " Users");

            return deleted.ToString();
        }

        /// <summary>
        /// The current user's UserSessionId, typically set by the controller
        /// </summary>
        /// <remarks>
        /// Guaranteed to exist and be a non-Empty Guid
        /// </remarks>
        public Guid UserSessionId {
          get { return _userSessionId; }
          set {
            _userSessionId = (value == Guid.Empty) ? _guestUserSessionId : value;
          }
        }

        #region Private

        private NorthwindContext Context { get { return _contextProvider.Context; } }

        private void PrepareSaveGuard() {
          if (_entitySaveGuard == null) {
            _entitySaveGuard = new NorthwindEntitySaveGuard { UserSessionId = UserSessionId };
            _contextProvider.BeforeSaveEntityDelegate += _entitySaveGuard.BeforeSaveEntity;
            _contextProvider.BeforeSaveEntitiesDelegate += _entitySaveGuard.BeforeSaveEntities;
            _contextProvider.AfterSaveEntitiesDelegate += _entitySaveGuard.AfterSaveEntities;
          }
        }
 
        private Guid _userSessionId = _guestUserSessionId;

        private IQueryable<T> ForCurrentUser<T>(IQueryable<T> query) where T : class, ISaveable
        {
            return query.Where(x => x.UserSessionId == null || x.UserSessionId == UserSessionId);
        }

        private readonly EFContextProvider<NorthwindContext> _contextProvider;
        private NorthwindEntitySaveGuard _entitySaveGuard;

        private const string _guestUserSessionIdName = "12345678-9ABC-DEF0-1234-56789ABCDEF0";
        private static readonly Guid _guestUserSessionId = new Guid(_guestUserSessionIdName);

        #endregion
    }
}