﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Primitives;
using Microsoft.Health.Core.Features.Security;
using Microsoft.Health.Fhir.Core.Features.Compartment;
using Microsoft.Health.Fhir.Core.Features.Context;
using Microsoft.Health.Fhir.Core.Features.Definition;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Features.Search;
using Microsoft.Health.Fhir.Core.Features.Search.SearchValues;
using Microsoft.Health.Fhir.Core.Models;
using Microsoft.Health.Fhir.Tests.Common;
using NSubstitute;
using Xunit;

namespace Microsoft.Health.Fhir.Core.UnitTests.Features.Persistence
{
    public class ResourceWrapperFactoryTests
    {
        private readonly IRawResourceFactory _rawResourceFactory;
        private readonly ISearchIndexer _searchIndexer;
        private readonly IFhirRequestContextAccessor _fhirRequestContextAccessor;
        private readonly IClaimsExtractor _claimsExtractor;
        private readonly ICompartmentIndexer _compartmentIndexer;
        private readonly ISearchParameterDefinitionManager _searchParameterDefinitionManager;
        private readonly ResourceWrapperFactory _resourceWrapperFactory;

        private readonly SearchParameterInfo _nameSearchParameterInfo;
        private readonly SearchParameterInfo _addressSearchParameterInfo;
        private readonly SearchParameterInfo _ageSearchParameterInfo;

        public ResourceWrapperFactoryTests()
        {
            var serializer = new FhirJsonSerializer();
            _rawResourceFactory = new RawResourceFactory(serializer);

            var dummyRequestContext = new FhirRequestContext(
                "POST",
                "https://localhost/Patient",
                "https://localhost/",
                Guid.NewGuid().ToString(),
                new Dictionary<string, StringValues>(),
                new Dictionary<string, StringValues>());
            _fhirRequestContextAccessor = Substitute.For<IFhirRequestContextAccessor>();
            _fhirRequestContextAccessor.FhirRequestContext.Returns(dummyRequestContext);

            _claimsExtractor = Substitute.For<IClaimsExtractor>();
            _compartmentIndexer = Substitute.For<ICompartmentIndexer>();
            _searchIndexer = Substitute.For<ISearchIndexer>();

            _searchParameterDefinitionManager = Substitute.For<ISearchParameterDefinitionManager>();
            _searchParameterDefinitionManager.GetSearchParameterHashForResourceType(Arg.Any<string>()).Returns("hash");

            _resourceWrapperFactory = new ResourceWrapperFactory(
                _rawResourceFactory,
                _fhirRequestContextAccessor,
                _searchIndexer,
                _claimsExtractor,
                _compartmentIndexer,
                _searchParameterDefinitionManager);

            _nameSearchParameterInfo = new SearchParameterInfo("name", ValueSets.SearchParamType.String, new Uri("https://localhost/searchParameter/name"));
            _addressSearchParameterInfo = new SearchParameterInfo("address-city", ValueSets.SearchParamType.String, new Uri("https://localhost/searchParameter/address-city"));
            _ageSearchParameterInfo = new SearchParameterInfo("age", ValueSets.SearchParamType.Number, new Uri("https://localhost/searchParameter/age"));
        }

        [Fact]
        public void GivenMultipleStringSearchValueForOneParameter_WhenCreate_ThenMinMaxValuesSetCorrectly()
        {
            var searchIndexEntry1 = new SearchIndexEntry(_nameSearchParameterInfo, new StringSearchValue("alpha"));
            var searchIndexEntry2 = new SearchIndexEntry(_nameSearchParameterInfo, new StringSearchValue("beta"));
            var searchIndexEntry3 = new SearchIndexEntry(_nameSearchParameterInfo, new StringSearchValue("gamma"));
            _searchIndexer
                .Extract(Arg.Any<ResourceElement>())
                .Returns(new List<SearchIndexEntry>() { searchIndexEntry1, searchIndexEntry2, searchIndexEntry3 });

            ResourceElement resource = Samples.GetDefaultPatient(); // Resource does not matter for this test.
            ResourceWrapper resourceWrapper = _resourceWrapperFactory.Create(resource, deleted: false, keepMeta: false);

            foreach (SearchIndexEntry searchEntry in resourceWrapper.SearchIndices)
            {
                switch (searchEntry.Value.ToString())
                {
                    case "alpha":
                        Assert.True(searchEntry.Value.IsMin);
                        Assert.False(searchEntry.Value.IsMax);
                        break;
                    case "beta":
                        Assert.False(searchEntry.Value.IsMin);
                        Assert.False(searchEntry.Value.IsMax);
                        break;
                    case "gamma":
                        Assert.False(searchEntry.Value.IsMin);
                        Assert.True(searchEntry.Value.IsMax);
                        break;
                    default:
                        throw new Exception("Unexpected value");
                }
            }
        }

        [Fact]
        public void GivenOneStringSearchValueForEachParameter_WhenCreate_ThenBothMinMaxSetToTrue()
        {
            var searchIndexEntry1 = new SearchIndexEntry(_nameSearchParameterInfo, new StringSearchValue("alpha"));
            var searchIndexEntry2 = new SearchIndexEntry(_addressSearchParameterInfo, new StringSearchValue("redmond"));
            _searchIndexer
                .Extract(Arg.Any<ResourceElement>())
                .Returns(new List<SearchIndexEntry>() { searchIndexEntry1, searchIndexEntry2});

            ResourceElement resource = Samples.GetDefaultPatient(); // Resource does not matter for this test.
            ResourceWrapper resourceWrapper = _resourceWrapperFactory.Create(resource, deleted: false, keepMeta: false);

            foreach (SearchIndexEntry searchEntry in resourceWrapper.SearchIndices)
            {
                switch (searchEntry.Value.ToString())
                {
                    case "alpha":
                        Assert.True(searchEntry.Value.IsMin);
                        Assert.True(searchEntry.Value.IsMax);
                        break;
                    case "redmond":
                        Assert.True(searchEntry.Value.IsMin);
                        Assert.True(searchEntry.Value.IsMax);
                        break;
                    default:
                        throw new Exception("Unexpected value");
                }
            }
        }

        [Fact]
        public void GivenNonStringSearchValue_WhenCreate_ThenMinMaxValuesAreNotSet()
        {
            var searchIndexEntry1 = new SearchIndexEntry(_nameSearchParameterInfo, new StringSearchValue("alpha"));
            var searchIndexEntry2 = new SearchIndexEntry(_ageSearchParameterInfo, new NumberSearchValue(25));
            _searchIndexer
                .Extract(Arg.Any<ResourceElement>())
                .Returns(new List<SearchIndexEntry>() { searchIndexEntry1, searchIndexEntry2 });

            ResourceElement resource = Samples.GetDefaultPatient(); // Resource does not matter for this test.
            ResourceWrapper resourceWrapper = _resourceWrapperFactory.Create(resource, deleted: false, keepMeta: false);

            foreach (SearchIndexEntry searchEntry in resourceWrapper.SearchIndices)
            {
                switch (searchEntry.Value.ToString())
                {
                    case "alpha":
                        Assert.True(searchEntry.Value.IsMin);
                        Assert.True(searchEntry.Value.IsMax);
                        break;
                    case "25":
                        Assert.False(searchEntry.Value.IsMin);
                        Assert.False(searchEntry.Value.IsMax);
                        break;
                    default:
                        throw new Exception("Unexpected value");
                }
            }
        }
    }
}