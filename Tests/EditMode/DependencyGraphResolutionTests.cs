using System.Collections.Generic;
using AeLa.Utilities.SceneDeps.Tests.Shared;
using NUnit.Framework;

namespace AeLa.Utilities.SceneDeps.Tests.EditMode
{
	public class DependencyGraphResolutionTests
	{
		[Test]
		public void BasicGraph_OrderIsCorrect()
		{
			///       A
			///     /   \
			///   B(1)  C(2)
			///   /      \
			/// D(0)     E(1)
			///           \
			///            F(0)
			var dependencyLists = new List<ISceneDependencyProvider>
			{
				new DummySceneDependencyProvider("A", "B", "C"),
				new DummySceneDependencyProvider("B", "D"),
				new DummySceneDependencyProvider("C", "E"),
				new DummySceneDependencyProvider("E", "F")
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
		}

		[Test]
		public void DuplicateDependencies_ResolvesCorrectly()
		{
			///       A _____
			///     /  \     \
			///   B(0)  C(0) C(ignored)
			var dependencyLists = new List<ISceneDependencyProvider>
			{
				new DummySceneDependencyProvider("A", "B", "C", "C"),
			};

			var groups = SceneDependencies.GetDependencies("A", dependencyLists);

			Assert.AreEqual(1, groups.Count);

			// It's annoying to check if an IReadOnlyList contains an element, so we just dump results into lists to test
			var group = new List<string>(groups.GetGroup(0));
			Assert.AreEqual(2, group.Count);
			Assert.Contains("B", group);
			Assert.Contains("C", group);
		}

		[Test]
		public void SharedDependencies_OrderIsCorrect()
		{
			///       A
			///     /   \
			///   B(2)  C(2)
			///   /  \  /
			/// D(0)  E(1)
			///        \
			///        F(0)
			var dependencyLists = new List<ISceneDependencyProvider>
			{
				new DummySceneDependencyProvider("A", "B", "C"),
				new DummySceneDependencyProvider("B", "D", "E"),
				new DummySceneDependencyProvider("C", "E"),
				new DummySceneDependencyProvider("E", "F")
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
					"A", new List<ISceneDependencyProvider>
					{
						new DummySceneDependencyProvider("A", "B", "C"),
						new DummySceneDependencyProvider("B", "D", "E"),
						new DummySceneDependencyProvider("C", "E"),
						new DummySceneDependencyProvider("E", "F"),
						new DummySceneDependencyProvider("F", "C")
					}
				)
			);
		}

		[Test]
		public void UnreliableList_OnlyFirstResultUsed()
		{
			///       A
			///     /   \
			///   B(2)  C(2)
			///   /  \  /
			/// D(0)  E(1)
			///        \
			///        F(0)
			var dependencyLists = new List<ISceneDependencyProvider>
			{
				new DummySceneDependencyProvider("A", "B", "C"),
				new DummySceneDependencyProvider("B", "D", "E"),
				new DummySceneDependencyProvider("C", "E"),
				new UnreliableSceneDependencyProvider("E", "F")
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
		}
	}
}