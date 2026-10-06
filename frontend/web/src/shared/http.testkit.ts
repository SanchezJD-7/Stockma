import { AxiosError } from 'axios'
import type { InternalAxiosRequestConfig, AxiosResponse } from 'axios'
import { api } from './http'

const originalAdapter = api.defaults.adapter

export interface MockReply {
  status: number
  data: unknown
}

export function json(status: number, data: unknown): MockReply {
  return { status, data }
}

export function mockApi(...replies: MockReply[]): InternalAxiosRequestConfig[] {
  const queue = [...replies]
  const requests: InternalAxiosRequestConfig[] = []

  api.defaults.adapter = async (config) => {
    requests.push(config)
    const reply = queue.shift()

    if (!reply) {
      throw new Error(
        `mockApi: sin respuesta encolada para ${String(config.method)} ${String(config.url)}`,
      )
    }

    const response: AxiosResponse = {
      data: reply.data,
      status: reply.status,
      statusText: String(reply.status),
      headers: {},
      config,
    }

    if (reply.status >= 200 && reply.status < 300) {
      return response
    }

    throw new AxiosError(
      `Request failed with status code ${reply.status}`,
      'ERR_BAD_REQUEST',
      config,
      null,
      response,
    )
  }

  return requests
}

export function mockPending(): InternalAxiosRequestConfig[] {
  const requests: InternalAxiosRequestConfig[] = []

  api.defaults.adapter = (config) => {
    requests.push(config)
    return new Promise<AxiosResponse>(() => {})
  }

  return requests
}

export function restoreHttp(): void {
  api.defaults.adapter = originalAdapter
}
