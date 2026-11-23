using UiPath.CodedWorkflows;
using System;

namespace AfternoonWalk
{
    public class GoogleDocsFactory
    {
        public GoogleDocsFactory(ICodedWorkflowsServiceContainer resolver)
        {
        }
    }

    public class DriveFactory
    {
        public DriveFactory(ICodedWorkflowsServiceContainer resolver)
        {
        }
    }

    public class GoogleFormsFactory
    {
        public GoogleFormsFactory(ICodedWorkflowsServiceContainer resolver)
        {
        }
    }

    public class GmailFactory
    {
        public UiPath.GSuite.Activities.Api.GmailConnection My_Workspace_lorenzofeldens_gmail_com { get; set; }

        public GmailFactory(ICodedWorkflowsServiceContainer resolver)
        {
            My_Workspace_lorenzofeldens_gmail_com = new UiPath.GSuite.Activities.Api.GmailConnection("d660fbe8-6939-4776-bf9e-fa0e170e3a0b", resolver);
        }
    }

    public class GoogleSheetsFactory
    {
        public GoogleSheetsFactory(ICodedWorkflowsServiceContainer resolver)
        {
        }
    }

    public class GoogleTasksFactory
    {
        public GoogleTasksFactory(ICodedWorkflowsServiceContainer resolver)
        {
        }
    }

    public class GoogleWorkspaceFactory
    {
        public GoogleWorkspaceFactory(ICodedWorkflowsServiceContainer resolver)
        {
        }
    }
}