using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Configuration;

using Microsoft.Synchronization;
using Microsoft.Synchronization.Files; 

namespace uSync.DiskEdition
{
    class Program
    {
        /// <summary>
        /// uSync.DiskEdition - runs a physical sync of two sites.
        /// 
        /// just playing around with replication and the Microsoft Sync
        /// framework. this will keep to umbraco disk installs in sync.
        /// 
        /// ironicially the default app.config in this app doesn't sync usync
        /// 
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            string source1 = ConfigurationManager.AppSettings["source1"];
            string source2 = ConfigurationManager.AppSettings["source2"];

            Console.WriteLine("Source 1 : {0}", source1);
            Console.WriteLine("Source 2 : {0}", source2);

            try
            {
                FileSyncOptions options =
                    FileSyncOptions.ExplicitDetectChanges
                    | FileSyncOptions.None;

                FileSyncScopeFilter filter = new FileSyncScopeFilter();

                Console.Write("Adding Exclusions : ");

                if (!String.IsNullOrEmpty(ConfigurationManager.AppSettings["excludeDirectories"]))
                {
                    foreach (string folder in ConfigurationManager.AppSettings["excludeDirectories"].Split(','))
                    {
                        filter.SubdirectoryExcludes.Add(folder);
                        Console.Write("{0},", folder);
                    }
                }

                if (!String.IsNullOrEmpty(ConfigurationManager.AppSettings["excludeFiles"]))
                {
                    foreach (string folder in ConfigurationManager.AppSettings["excludeFiles"].Split(','))
                    {
                        filter.FileNameExcludes.Add(folder);
                        Console.Write("{0},", folder);
                    }
                }
                Console.WriteLine(" Added");

                Console.WriteLine("Detecting Changes "); 
                // detect (do this here, or it gets done to many times
                DetectChanges(source1, filter, options);
                DetectChanges( source2, filter, options) ; 

                string direction = "both" ; 
                if ( !String.IsNullOrEmpty(ConfigurationManager.AppSettings["direction"] ) ) 
                    direction = ConfigurationManager.AppSettings["direction"] ;

                Console.WriteLine("Direction {0}", direction); 

                if ((direction == "AB") || (direction == "both"))
                {
                    SyncFileSystems(source1, source2, filter, options);
                }

                if ((direction == "BA") || (direction == "both"))
                {
                    SyncFileSystems(source2, source1, filter, options);
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine("Pete Tong\n {0}", ex.ToString());
            }

        }

        public static void DetectChanges(string root, FileSyncScopeFilter filter, FileSyncOptions options)
        {
            FileSyncProvider provider = null;
            try
            {

                provider = new FileSyncProvider(root, filter, options);
                provider.DetectChanges();
            }
            finally
            {
                if (provider != null)
                    provider.Dispose();
            }
        }

        public static void SyncFileSystems(
            string source,
            string dest,
            FileSyncScopeFilter filter,
            FileSyncOptions options)
        {
            FileSyncProvider sourceProvider = null;
            FileSyncProvider destProvider = null;

            try
            {
                sourceProvider = new FileSyncProvider(source, filter, options);
                destProvider = new FileSyncProvider(dest, filter, options);

                SyncOrchestrator agent = new SyncOrchestrator();
                agent.LocalProvider = sourceProvider;
                agent.RemoteProvider = destProvider;
                agent.Direction = SyncDirectionOrder.Upload;

                destProvider.AppliedChange += destProvider_AppliedChange;
                destProvider.SkippedChange += destProvider_SkippedChange;

                // call backs so we can monitor...
                SyncCallbacks destCallbacks = destProvider.DestinationCallbacks;

                destCallbacks.ItemConflicting += destCallbacks_ItemConflicting;
                destCallbacks.ItemConstraint += destCallbacks_ItemConstraint;

                Console.WriteLine("sync to {0} from {1}", source, dest); 
                agent.Synchronize(); 
            }
            finally {
                if (sourceProvider != null)
                    sourceProvider.Dispose();
                if ( destProvider != null ) 
                    destProvider.Dispose() ; 
            }
        }

        static void destProvider_SkippedChange(object sender, SkippedChangeEventArgs e)
        {
            Console.WriteLine("-- Skipped applying " + e.ChangeType.ToString().ToUpper()
                 + " for " + (!string.IsNullOrEmpty(e.CurrentFilePath) ?
                               e.CurrentFilePath : e.NewFilePath) + " due to error");

            if (e.Exception != null)
                Console.WriteLine("   [" + e.Exception.Message + "]"); 
        }

        static void destProvider_AppliedChange(object sender, AppliedChangeEventArgs e)
        {
            switch (e.ChangeType)
            {
                case ChangeType.Create:
                    Console.WriteLine("-- Applied CREATE for file " + e.NewFilePath);
                    break;
                case ChangeType.Delete:
                    Console.WriteLine("-- Applied DELETE for file " + e.OldFilePath);
                    break;
                case ChangeType.Update:
                    Console.WriteLine("-- Applied OVERWRITE for file " + e.OldFilePath);
                    break;
                case ChangeType.Rename:
                    Console.WriteLine("-- Applied RENAME for file " + e.OldFilePath +
                                      " as " + e.NewFilePath);
                    break;
            }
        }

        static void destCallbacks_ItemConstraint(object sender, ItemConstraintEventArgs e)
        {
            e.SetResolutionAction(ConstraintConflictResolutionAction.SourceWins);
            Console.WriteLine("-- Constraint conflict detected for item " + e.DestinationChange.ItemId.ToString());
        }

        static void destCallbacks_ItemConflicting(object sender, ItemConflictingEventArgs e)
        {
            e.SetResolutionAction(ConflictResolutionAction.SourceWins);
            Console.WriteLine("-- Concurrency conflict detected for item " + e.DestinationChange.ItemId.ToString());
        }
    }
}
