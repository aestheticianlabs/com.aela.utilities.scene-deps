using System.Collections.Generic;
using NUnit.Framework;

namespace AeLa.Utilities.SceneDeps.Tests.EditMode
{
	public class DependencyGraphResolutionTests
	{
		[Test]
		public void BasicGraph_OrderIsCorrect()
		{
			///     A(3)
			///     /  \
			///   B(1)  C(2)
			///   /      \
			/// D(0)     E(1)
			///           \
			///            F(0)
			var dependencyLists = new List<IDependencyList>
			{
				new DummyDependencyList("A", "B", "C"),
				new DummyDependencyList("B", "D"),
				new DummyDependencyList("C", "E"),
				new DummyDependencyList("E", "F")
			};

			var groups = SceneDependencies.GetDependencies("A", dependencyLists);

			// It's annoying to check if an IReadOnlyList contains an element, so we just dump results into lists to test
			var groupLists = new List<string>[groups.Count];
			for (int i = 0; i < groupLists.Length; i++)
			{
				groupLists[i] = new(groups.GetGroup(i));
			}

			Assert.Contains("F", groupLists[0]);
			Assert.Contains("D", groupLists[0]);
			Assert.Contains("E", groupLists[1]);
			Assert.Contains("B", groupLists[1]);
			Assert.Contains("C", groupLists[2]);
			Assert.Contains("A", groupLists[3]);
		}

		[Test]
		public void SharedDependencies_OrderIsCorrect()
		{
			///      A(3)
			///     /   \
			///   B(2)  C(2)
			///   /  \  /
			/// D(0)  E(1)
			///        \
			///        F(0)
			var dependencyLists = new List<IDependencyList>
			{
				new DummyDependencyList("A", "B", "C"),
				new DummyDependencyList("B", "D", "E"),
				new DummyDependencyList("C", "E"),
				new DummyDependencyList("E", "F")
			};

			var groups = SceneDependencies.GetDependencies("A", dependencyLists);

			// It's annoying to check if an IReadOnlyList contains an element, so we just dump results into lists to test
			var groupLists = new List<string>[groups.Count];
			for (int i = 0; i < groupLists.Length; i++)
			{
				groupLists[i] = new(groups.GetGroup(i));
			}

			Assert.Contains("F", groupLists[0]);
			Assert.Contains("D", groupLists[0]);
			Assert.Contains("E", groupLists[1]);
			Assert.Contains("B", groupLists[2]);
			Assert.Contains("C", groupLists[2]);
			Assert.Contains("A", groupLists[3]);
		}

		[Test]
		public void Cycle_ExceptionThrown()
		{
			///       A
			///     /   \
			///    B     C
			///   /  \  /
			///  D    E
			///        \
			///         F
			///        /
			///       C!
			Assert.Throws<CyclicDependenciesException>(() =>
				SceneDependencies.GetDependencies(
					"A", new List<IDependencyList>
					{
						new DummyDependencyList("A", "B", "C"),
						new DummyDependencyList("B", "D", "E"),
						new DummyDependencyList("C", "E"),
						new DummyDependencyList("E", "F"),
						new DummyDependencyList("F", "C")
					}
				)
			);
		}

		[Test]
		public void UnreliableList_OnlyFirstResultUsed()
		{
			///      A(3)
			///     /   \
			///   B(2)  C(2)
			///   /  \  /
			/// D(0)  E(1)
			///        \
			///        F(0)
			var dependencyLists = new List<IDependencyList>
			{
				new DummyDependencyList("A", "B", "C"),
				new DummyDependencyList("B", "D", "E"),
				new DummyDependencyList("C", "E"),
				new UnreliableDependencyList("E", "F")
			};

			var groups = SceneDependencies.GetDependencies("A", dependencyLists);

			// It's annoying to check if an IReadOnlyList contains an element, so we just dump results into lists to test
			var groupLists = new List<string>[groups.Count];
			for (int i = 0; i < groupLists.Length; i++)
			{
				groupLists[i] = new(groups.GetGroup(i));
			}

			Assert.Contains("F", groupLists[0]);
			Assert.Contains("D", groupLists[0]);
			Assert.Contains("E", groupLists[1]);
			Assert.Contains("B", groupLists[2]);
			Assert.Contains("C", groupLists[2]);
			Assert.Contains("A", groupLists[3]);
		}
	}
}