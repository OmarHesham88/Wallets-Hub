import { describe, expect, it } from "vitest";
import { queryString } from "./api";

describe("queryString", () => {
  it("omits empty filters and safely encodes useful values", () => {
    expect(queryString({ search: "Nadia H", provider: "Vodafone Cash", page: 2, empty: "", missing: undefined }))
      .toBe("?search=Nadia+H&provider=Vodafone+Cash&page=2");
  });

  it("keeps explicit false filter values", () => {
    expect(queryString({ missingSender: false })).toBe("?missingSender=false");
  });
});
