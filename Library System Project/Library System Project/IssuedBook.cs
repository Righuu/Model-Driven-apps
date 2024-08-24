using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Library_System_Project
{
    public class IssuedBook: IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            if (context.MessageName == "Create")
            {
                Entity entity = (Entity)context.InputParameters["Target"];
                var book = entity.GetAttributeValue<EntityReference>("lib_book");

                    var bookRecordRetreive = service.Retrieve(book.LogicalName, book.Id, new ColumnSet("lib_totalstocks", "lib_stocks"));
                    Entity bookRecord = new Entity(book.LogicalName, book.Id);
                    bookRecord["lib_totalstocks"] = bookRecordRetreive.GetAttributeValue<int>("lib_totalstocks") + 1;
                    bookRecord["lib_stocks"] = bookRecordRetreive.GetAttributeValue<int>("lib_stocks") + 1;
                    service.Update(bookRecord);
            }
            else if (context.MessageName == "Update")
            {
                Entity postImageBookStack = (Entity)context.PostEntityImages["imaging"];
                var postBook = postImageBookStack.GetAttributeValue<EntityReference>("lib_book");
                if (postImageBookStack.Contains("lib_bookissuedto"))
                {
                    var student = postImageBookStack.GetAttributeValue<EntityReference>("lib_bookissuedto");
                    QueryExpression issuedBooks = new QueryExpression("lib_library");
                    issuedBooks.ColumnSet.AddColumns("lib_issuedbook", "lib_student");
                    FilterExpression filter1 = new FilterExpression(LogicalOperator.And);
                    filter1.AddCondition("lib_issuedbook", ConditionOperator.Equal, postImageBookStack.Id);
                    filter1.AddCondition("lib_student", ConditionOperator.Equal, student.Id);
                    filter1.AddCondition("lib_submitbookstatus", ConditionOperator.Equal, false);
                    issuedBooks.Criteria.AddFilter(filter1);

                    EntityCollection entityCollection = service.RetrieveMultiple(issuedBooks);
                    if (entityCollection.Entities.Count == 0)
                    {
                        var member = service.Retrieve(student.LogicalName, student.Id, new ColumnSet("lib_membertype"));
                        var memberType = member.GetAttributeValue<OptionSetValue>("lib_membertype");
                        Entity bookIssue = new Entity("lib_library");
                        bookIssue["lib_issuedbook"] = new EntityReference(postImageBookStack.LogicalName, postImageBookStack.Id);
                        bookIssue["lib_student"] = student;
                        if(memberType.Value == 0)
                        {
                            bookIssue["lib_issueddays"] = 30;
                            bookIssue["lib_duedate"] = DateTime.Now.Date.AddDays(30);
                            bookIssue["lib_perdaycharge"] = new Money(10m);
                            bookIssue["lib_daysleft"] = 30;
                            bookIssue["lib_membertype"] = new OptionSetValue(0);
                        }
                        else if (memberType.Value == 1)
                        {
                            bookIssue["lib_issueddays"] = 90;
                            bookIssue["lib_duedate"] = DateTime.Now.Date.AddDays(90);
                            bookIssue["lib_perdaycharge"] = new Money(0m);
                            bookIssue["lib_daysleft"] = 90;
                            bookIssue["lib_membertype"] = new OptionSetValue(1);
                        }
                        else
                        {
                            return;
                        }
                        service.Create(bookIssue);

                        Entity bookStock = new Entity(postImageBookStack.LogicalName, postImageBookStack.Id);
                        bookStock["lib_bookissuedstatus"] = true;
                        service.Update(bookStock);

                        Entity studentData = new Entity(student.LogicalName, student.Id);
                        studentData["lib_status"] = true;
                        service.Update(studentData);
                    }
                }
                // Available Book and Total Books count
                QueryExpression postBookStockquery = new QueryExpression("lib_bookstocks");
                postBookStockquery.ColumnSet.AddColumns("lib_bookissuedstatus", "lib_book");
                FilterExpression postBookfilter = new FilterExpression(LogicalOperator.And);
                postBookfilter.AddCondition("lib_book", ConditionOperator.Equal, postBook.Id);
                postBookfilter.AddCondition("lib_bookissuedstatus", ConditionOperator.Equal, false);
                postBookStockquery.Criteria.AddFilter(postBookfilter);

                EntityCollection postBookStocks = service.RetrieveMultiple(postBookStockquery);
                if (postBookStocks.Entities.Count >= 0)
                {
                    Entity postBookRecord = new Entity(postBook.LogicalName, postBook.Id);
                    postBookRecord["lib_stocks"] = postBookStocks.Entities.Count;
                    service.Update(postBookRecord);
                }
            }
            else if(context.MessageName == "Delete")
            {
                Entity preImageBookStack = (Entity)context.PreEntityImages["imaging"];
                var preBook = preImageBookStack.GetAttributeValue<EntityReference>("lib_book");
                var preBookIssuedStatus = preImageBookStack.GetAttributeValue<bool>("lib_bookissuedstatus");

                QueryExpression preBookStockquery = new QueryExpression("lib_bookstocks");
                preBookStockquery.ColumnSet.AddColumns("lib_bookissuedstatus", "lib_book");
                FilterExpression prefilter = new FilterExpression(LogicalOperator.And);
                prefilter.AddCondition("lib_book", ConditionOperator.Equal, preBook.Id);
                preBookStockquery.Criteria.AddFilter(prefilter);

                EntityCollection preBookStocks = service.RetrieveMultiple(preBookStockquery);
                if (preBookStocks.Entities.Count >= 0)
                {
                    var prebookRecordRetreive = service.Retrieve(preBook.LogicalName, preBook.Id, new ColumnSet("lib_totalstocks", "lib_stocks"));
                    var lib_stocks = prebookRecordRetreive.GetAttributeValue<int>("lib_stocks");
                    Entity preBookRecord = new Entity(preBook.LogicalName, preBook.Id);
                    preBookRecord["lib_totalstocks"] = preBookStocks.Entities.Count;
                    if (!preBookIssuedStatus)
                    {
                        if (lib_stocks > 0)
                            preBookRecord["lib_stocks"] = prebookRecordRetreive.GetAttributeValue<int>("lib_stocks") - 1;
                        else
                            preBookRecord["lib_stocks"] = 0;
                    }
                    service.Update(preBookRecord);
                }
            }
        }
    }
}
