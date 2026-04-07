export interface WorkItemAttachment {
  id: string
  fileName: string
  contentLength: number
  uploadedAt: string
  contentType: string
  contentUrl: string
  markdownReference: string
  isImage: boolean
}
