using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.Tests.Mocks;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    sealed class QueryPlanTraceViewModelTester : QueryPlanTraceViewModel
    {
        public QueryPlanTraceViewModelTester(IEventAggregator eventAggregator, IGlobalOptions globalOptions, IWindowManager windowManager) : base(eventAggregator, globalOptions, windowManager)
        {
        }

		public void TestPrepareLogicalQueryPlan(string queryPlan)
        {
			base.PrepareLogicalQueryPlan(queryPlan);
        }
    }

    [TestClass]
    public class QueryPlanTests
    {
		private IGlobalOptions mockOptions;
		private IWindowManager mockWindowManager;
		private IEventAggregator mockEventAggregator;

		[TestInitialize]
		public void TestSetup()
		{
			mockOptions = Substitute.For<IGlobalOptions>();
			mockEventAggregator = new Mocks.MockEventAggregator();
			mockWindowManager = Substitute.For<IWindowManager>();
		}

		[TestMethod]
        public void LogicalQueryPlanTest()
        {
			string rawPlan = @"Order: RelLogOp DependOnCols()() 0-3 RequiredCols(0, 1, 2, 3)('DimProduct'[Class], 'DimProduct'[Color], ''[Total Reseller Sales], ''[Total Reseller Orders])
	GroupSemiJoin: RelLogOp DependOnCols()() 0-3 RequiredCols(0, 1, 2, 3)('DimProduct'[Class], 'DimProduct'[Color], ''[Total Reseller Sales], ''[Total Reseller Orders])
		GroupSemiJoinFilterCluster: RelLogOp DependOnCols()() 0-1 RequiredCols(0, 1)('DimProduct'[Color], 'DimProduct'[Class])
			Filter: RelLogOp DependOnCols()() 0-0 RequiredCols(0)('DimProduct'[Color])
				Scan_Vertipaq: RelLogOp DependOnCols()() 0-0 RequiredCols(0)('DimProduct'[Color])
				GreaterThanOrEqualTo: ScaLogOp DependOnCols(0)('DimProduct'[Color]) Boolean DominantValue=NONE
					SEARCH: ScaLogOp DependOnCols(0)('DimProduct'[Color]) Integer DominantValue=NONE
						Constant: ScaLogOp DependOnCols()() String DominantValue=e
						'DimProduct'[Color]: ScaLogOp DependOnCols(0)('DimProduct'[Color]) String DominantValue=NONE
						Constant: ScaLogOp DependOnCols()() Integer DominantValue=1
						Constant: ScaLogOp DependOnCols()() Integer DominantValue=0
					Constant: ScaLogOp DependOnCols()() Integer DominantValue=1
			Scan_Vertipaq: RelLogOp DependOnCols(0)('DimProduct'[Color]) 1-1 RequiredCols(0, 1)('DimProduct'[Color], 'DimProduct'[Class])
		Sum_Vertipaq: ScaLogOp MeasureRef=[Total Reseller Sales] DependOnCols(0, 1)('DimProduct'[Color], 'DimProduct'[Class]) Currency DominantValue=BLANK
			Scan_Vertipaq: RelLogOp DependOnCols(0, 1)('DimProduct'[Color], 'DimProduct'[Class]) 2-285 RequiredCols(0, 1, 22)('DimProduct'[Color], 'DimProduct'[Class], 'FactResellerSales'[SalesAmount])
			'FactResellerSales'[SalesAmount]: ScaLogOp DependOnCols(22)('FactResellerSales'[SalesAmount]) Currency DominantValue=NONE
		Sum_Vertipaq: ScaLogOp MeasureRef=[Total Reseller Orders] DependOnCols(0, 1)('DimProduct'[Color], 'DimProduct'[Class]) Integer DominantValue=BLANK
			Scan_Vertipaq: RelLogOp DependOnCols(0, 1)('DimProduct'[Color], 'DimProduct'[Class]) 2-285 RequiredCols(0, 1, 15)('DimProduct'[Color], 'DimProduct'[Class], 'FactResellerSales'[OrderQuantity])
			'FactResellerSales'[OrderQuantity]: ScaLogOp DependOnCols(15)('FactResellerSales'[OrderQuantity]) Integer DominantValue=NONE
	ColPosition<'DimProduct'[Class]>: ScaLogOp DependOnCols(0)('DimProduct'[Class]) String DominantValue=NONE
	ColPosition<'DimProduct'[Color]>: ScaLogOp DependOnCols(1)('DimProduct'[Color]) String DominantValue=NONE
";
			var vm = new QueryPlanTraceViewModelTester(mockEventAggregator, mockOptions, mockWindowManager);
			vm.TestPrepareLogicalQueryPlan(rawPlan);

			Assert.AreEqual(21, vm.LogicalQueryPlanRows.Count);
			Assert.AreEqual(22, vm.LogicalQueryPlanRows[0].NextSiblingRowNumber, "Row 1");
			Assert.AreEqual(20, vm.LogicalQueryPlanRows[1].NextSiblingRowNumber, "Row 2");
			Assert.AreEqual(14, vm.LogicalQueryPlanRows[2].NextSiblingRowNumber, "Row 3");
			Assert.AreEqual(13, vm.LogicalQueryPlanRows[3].NextSiblingRowNumber, "Row 4");
			Assert.AreEqual(5, vm.LogicalQueryPlanRows[4].NextSiblingRowNumber, "Row 5");
			Assert.AreEqual(13, vm.LogicalQueryPlanRows[5].NextSiblingRowNumber, "Row 6");
			Assert.AreEqual(12, vm.LogicalQueryPlanRows[6].NextSiblingRowNumber, "Row 7");
			Assert.AreEqual(8, vm.LogicalQueryPlanRows[7].NextSiblingRowNumber,  "Row 8");
			Assert.AreEqual(9, vm.LogicalQueryPlanRows[8].NextSiblingRowNumber,  "Row 9");
			Assert.AreEqual(10, vm.LogicalQueryPlanRows[9].NextSiblingRowNumber, "Row 10");
			Assert.AreEqual(11, vm.LogicalQueryPlanRows[10].NextSiblingRowNumber,"Row 11");
			Assert.AreEqual(12, vm.LogicalQueryPlanRows[11].NextSiblingRowNumber,"Row 12");
			Assert.AreEqual(13, vm.LogicalQueryPlanRows[12].NextSiblingRowNumber,"Row 13");
			Assert.AreEqual(17, vm.LogicalQueryPlanRows[13].NextSiblingRowNumber,"Row 14");
			Assert.AreEqual(15, vm.LogicalQueryPlanRows[14].NextSiblingRowNumber,"Row 15");
			Assert.AreEqual(16, vm.LogicalQueryPlanRows[15].NextSiblingRowNumber,"Row 16");
			Assert.AreEqual(20, vm.LogicalQueryPlanRows[16].NextSiblingRowNumber,"Row 17");
			Assert.AreEqual(18, vm.LogicalQueryPlanRows[17].NextSiblingRowNumber,"Row 18");
			Assert.AreEqual(19, vm.LogicalQueryPlanRows[18].NextSiblingRowNumber,"Row 19");
			Assert.AreEqual(20, vm.LogicalQueryPlanRows[19].NextSiblingRowNumber,"Row 20");
			Assert.AreEqual(22, vm.LogicalQueryPlanRows[20].NextSiblingRowNumber,"Row 21");

		}

        [TestMethod]
		public void test2()
        {
			var rawPlan = @"GroupSemijoin: IterPhyOp LogOp=GroupSemiJoin IterCols(0, 1)('Product'[Brand], ''[Discount if greater than 3])
	Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Sum_Vertipaq IterCols(0)('Product'[Brand]) #Records=11 #KeyCols=75 #ValueCols=1
		ProjectionSpool<ProjectFusion<Copy>>: SpoolPhyOp #Records=11
			Cache: IterPhyOp #FieldCols=1 #ValueCols=1
				If: LookupPhyOp LogOp=If LookupCols(40, 42)('Sales'[Quantity], 'Sales'[Net Price]) Double
					GreaterThanOrEqualTo: LookupPhyOp LogOp=GreaterThanOrEqualTo LookupCols(40)('Sales'[Quantity]) Boolean
						ColValue<'Sales'[Quantity]>: LookupPhyOp LogOp=ColValue<'Sales'[Quantity]>'Sales'[Quantity] LookupCols(40)('Sales'[Quantity]) Integer
						Constant: LookupPhyOp LogOp=Constant Integer 3
					Multiply: LookupPhyOp LogOp=Multiply LookupCols(40, 42)('Sales'[Quantity], 'Sales'[Net Price]) Double
						ColValue<'Sales'[Quantity]>: LookupPhyOp LogOp=ColValue<'Sales'[Quantity]>'Sales'[Quantity] LookupCols(40)('Sales'[Quantity]) Integer
						ColValue<'Sales'[Net Price]>: LookupPhyOp LogOp=ColValue<'Sales'[Net Price]>'Sales'[Net Price] LookupCols(42)('Sales'[Net Price]) Double
					Multiply: LookupPhyOp LogOp=Multiply LookupCols(40, 42)('Sales'[Quantity], 'Sales'[Net Price]) Double
						Multiply: LookupPhyOp LogOp=Multiply LookupCols(40, 42)('Sales'[Quantity], 'Sales'[Net Price]) Double
							 ColValue<'Sales'[Quantity]>: LookupPhyOp LogOp=ColValue<'Sales'[Quantity]>'Sales'[Quantity] LookupCols(40)('Sales'[Quantity]) Integer
							 ColValue<'Sales'[Net Price]>: LookupPhyOp LogOp=ColValue<'Sales'[Net Price]>'Sales'[Net Price] LookupCols(42)('Sales'[Net Price]) Double
						Constant: LookupPhyOp LogOp=Constant Double 0.8
";
			var vm = new QueryPlanTraceViewModelTester(mockEventAggregator, mockOptions,mockWindowManager);
			vm.TestPrepareLogicalQueryPlan(rawPlan);

			Assert.AreEqual(16, vm.LogicalQueryPlanRows.Count);
			Assert.AreEqual(17, vm.LogicalQueryPlanRows[0].NextSiblingRowNumber, "Row 1");
			Assert.AreEqual(17, vm.LogicalQueryPlanRows[1].NextSiblingRowNumber, "Row 2");
			Assert.AreEqual(17, vm.LogicalQueryPlanRows[2].NextSiblingRowNumber, "Row 3");
			Assert.AreEqual(17, vm.LogicalQueryPlanRows[3].NextSiblingRowNumber, "Row 4");
			Assert.AreEqual(17, vm.LogicalQueryPlanRows[4].NextSiblingRowNumber, "Row 5");
			Assert.AreEqual(9, vm.LogicalQueryPlanRows[5].NextSiblingRowNumber, "Row 6");
			Assert.AreEqual(7, vm.LogicalQueryPlanRows[6].NextSiblingRowNumber, "Row 7");
			Assert.AreEqual(8, vm.LogicalQueryPlanRows[7].NextSiblingRowNumber, "Row 8");
			Assert.AreEqual(12, vm.LogicalQueryPlanRows[8].NextSiblingRowNumber, "Row 9");
			Assert.AreEqual(10, vm.LogicalQueryPlanRows[9].NextSiblingRowNumber, "Row 10");
			Assert.AreEqual(11, vm.LogicalQueryPlanRows[10].NextSiblingRowNumber, "Row 11");
			Assert.AreEqual(17, vm.LogicalQueryPlanRows[11].NextSiblingRowNumber, "Row 12");
			Assert.AreEqual(16, vm.LogicalQueryPlanRows[12].NextSiblingRowNumber, "Row 13");
			Assert.AreEqual(14, vm.LogicalQueryPlanRows[13].NextSiblingRowNumber, "Row 14");
			Assert.AreEqual(15, vm.LogicalQueryPlanRows[14].NextSiblingRowNumber, "Row 15");
			Assert.AreEqual(17, vm.LogicalQueryPlanRows[15].NextSiblingRowNumber, "Row 16");



		}

	}
}
