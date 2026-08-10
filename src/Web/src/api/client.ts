import createClient from "openapi-fetch";
import type { paths } from "./schema";
import { authenticatedFetch } from "./authentication";

export const api = createClient<paths>({
  baseUrl: globalThis.location?.origin ?? "http://localhost",
  fetch: authenticatedFetch,
});
